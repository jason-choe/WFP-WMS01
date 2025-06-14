// Services/DatabaseService.cs
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using WPF_WMS01.Models;
using System.Configuration; // System.Configuration.dll 참조 추가

namespace WPF_WMS01.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        // 랙 ID를 키로 하여 Rack 객체를 저장하는 캐시
        // 랙의 개수가 일정하고 추가/삭제가 없으므로 이 캐시가 유용합니다.
        private readonly Dictionary<int, Rack> _rackCache = new Dictionary<int, Rack>();
        private readonly object _cacheLock = new object(); // 스레드 안전을 위한 락 객체

        public DatabaseService()
        {
            // app.config에서 ConnectionString 읽어오기
            _connectionString = ConfigurationManager.ConnectionStrings["WmsBulletConnection"].ConnectionString;
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("WmsBulletConnection connection string is not found in app.config.");
            }
        }

        public async Task<List<Rack>> GetRackStatesAsync()
        {
            List<Rack> currentRacks = new List<Rack>(); // 현재 DB에서 읽어올 랙 목록 (캐시된 객체들로 구성)
            string query = "SELECT id as 'Id', rack_name as 'Title', rack_type AS 'RackType', bullet_type as 'BulletType', visible AS 'IsVisible', locked AS 'IsLocked', lot_number AS 'LotNumber', racked_at AS 'RackedAt' FROM RackState";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("Id")));
                            string title = reader.IsDBNull(reader.GetOrdinal("Title")) ? string.Empty : reader["Title"].ToString();
                            int rackType = reader.IsDBNull(reader.GetOrdinal("RackType")) ? 0 : Convert.ToInt32(reader["RackType"]);
                            int bulletType = reader.IsDBNull(reader.GetOrdinal("BulletType")) ? 0 : Convert.ToInt32(reader["BulletType"]);
                            bool isVisible = reader.IsDBNull(reader.GetOrdinal("IsVisible")) ? false : Convert.ToBoolean(reader["IsVisible"]);
                            bool isLocked = reader.IsDBNull(reader.GetOrdinal("IsLocked")) ? false : Convert.ToBoolean(reader["IsLocked"]);
                            string lotNumber = reader.IsDBNull(reader.GetOrdinal("LotNumber")) ? string.Empty : reader["LotNumber"].ToString();
                            DateTime? rackedAt = reader.IsDBNull(reader.GetOrdinal("RackedAt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("RackedAt"));

                            int imageIndex = rackType * 7 + bulletType;

                            Rack rack;
                            lock (_cacheLock) // 캐시 접근 시 락 걸기 (멀티스레드 환경 대비)
                            {
                                if (_rackCache.TryGetValue(id, out rack))
                                {
                                    // 캐시에 이미 존재하는 랙이면 속성 업데이트
                                    // 이 경우 Rack의 생성자는 호출되지 않고, setter만 호출됩니다.
                                    rack.Title = title;
                                    rack.RackType = rackType; // 이 setter 호출 시 _rackType은 -1이 아님 (기존 값에서 변경)
                                    rack.BulletType = bulletType;
                                    rack.IsVisible = isVisible;
                                    rack.IsLocked = isLocked;
                                    rack.LotNumber = lotNumber;
                                    rack.RackedAt = rackedAt;
                                }
                                else
                                {
                                    // 캐시에 없는 새로운 랙이면 생성 후 캐시에 추가
                                    // 이 경우는 최초 로드 시 또는 DB에 정말 새로운 랙이 추가된 경우 (현재 시나리오에서는 최초 1회만)
                                    rack = new Rack // 🚨 Rack() 생성자가 호출되는 유일한 시점 (최초 로드 시)
                                    {
                                        Id = id,
                                        Title = title,
                                        RackType = rackType,
                                        BulletType = bulletType,
                                        IsVisible = isVisible,
                                        IsLocked = isLocked,
                                        LotNumber = lotNumber,
                                        RackedAt = rackedAt
                                    };
                                    _rackCache.Add(id, rack);
                                }
                            }
                            currentRacks.Add(rack); // 현재 틱에 읽어온 랙 리스트에 추가 (캐시된/생성된 객체)
                        }
                    }
                }
            }
            // 랙의 개수가 일정하게 유지된다는 가정하에,
            // 더 이상 존재하지 않는 랙을 캐시에서 제거하는 복잡한 로직은 생략합니다.
            // MainViewModel이 RackList를 관리하고 있으므로, MainViewModel에서 없어진 랙을 처리합니다.

            return currentRacks;
        }

        // 랙 타입 (rack_type)을 업데이트하는 메서드 (새로 추가)
        public async Task UpdateRackTypeAsync(int rackId, int newRackType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("UPDATE RackState SET rack_type = @newRackType WHERE id = @rackId", connection);
                command.Parameters.AddWithValue("@newRackType", newRackType);
                command.Parameters.AddWithValue("@rackId", rackId);
                await command.ExecuteNonQueryAsync();
            }

            // DB 업데이트 후 캐시도 업데이트하여 일관성 유지
            lock (_cacheLock)
            {
                if (_rackCache.TryGetValue(rackId, out Rack rackToUpdate))
                {
                    rackToUpdate.RackType = newRackType; // Rack 모델의 setter 호출 (여기서는 _rackType이 -1로 초기화되지 않음)
                }
            }
        }

        // 랙 상태 업데이트 메서드 (필요시)
        // 필요에 따라 다른 업데이트 메서드 (예: RackType, BulletType, IsLocked 등을 한 번에 업데이트)
        public async Task UpdateRackStateAsync(
            int rackId,
            int newRackType,
            int newBulletType,
            bool newIsLocked
            )
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("UPDATE RackState SET rack_type = @rackType, bullet_type = @bulletType, locked = @isLocked WHERE id = @rackId", connection);
                command.Parameters.AddWithValue("@rackType", newRackType);
                command.Parameters.AddWithValue("@bulletType", newBulletType);
                command.Parameters.AddWithValue("@isLocked", newIsLocked);
                command.Parameters.AddWithValue("@rackId", rackId);
                await command.ExecuteNonQueryAsync();
            }

            // DB 업데이트 후 캐시도 업데이트
            lock (_cacheLock)
            {
                if (_rackCache.TryGetValue(rackId, out Rack rackToUpdate))
                {
                    rackToUpdate.RackType = newRackType;
                    rackToUpdate.BulletType = newBulletType;
                    rackToUpdate.IsLocked = newIsLocked;
                }
            }
        }

        // Lot Number 업데이트 메서드 (필요시)
        public async Task UpdateLotNumberAsync(int rackId, string newLotNumber)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand("UPDATE RackState SET lot_number = @lotNumber, racked_at = @rackedAt WHERE id = @rackId", connection);
                command.Parameters.AddWithValue("@lotNumber", newLotNumber);
                DateTime? tmpDateTime = String.IsNullOrEmpty(newLotNumber) ? (DateTime?)null : DateTime.Now;
                //command.Parameters.AddWithValue("@rackedAt", String.IsNullOrEmpty(newLotNumber) ? (DateTime?)null : DateTime.Now);
                command.Parameters.AddWithValue("@rackedAt", DateTime.Now);
                command.Parameters.AddWithValue("@rackId", rackId);
                await command.ExecuteNonQueryAsync();
            }

            // DB 업데이트 후 캐시도 업데이트
            lock (_cacheLock)
            {
                if (_rackCache.TryGetValue(rackId, out Rack rackToUpdate))
                {
                    rackToUpdate.LotNumber = newLotNumber;
                    rackToUpdate.RackedAt = String.IsNullOrEmpty(newLotNumber) ? null : DateTime.Now;
                }
            }
        }

    }
}