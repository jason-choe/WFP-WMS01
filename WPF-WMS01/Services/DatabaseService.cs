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
            List<Rack> racks = new List<Rack>();
            string query = "SELECT id as 'Id', rack_name as 'Title', rack_type*3 + bullet_type AS 'ImageIndex', visible AS 'IsVisible', locked AS 'IsLocked' FROM RackState";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // ID는 PRIMARY KEY이므로 NULL이 될 수 없다고 가정합니다.
                            string id = reader.GetInt64(reader.GetOrdinal("Id")).ToString();

                            // Title (string) 처리: NULL이면 빈 문자열 ''로 간주
                            string title = reader.IsDBNull(reader.GetOrdinal("Title")) ?
                                           string.Empty : reader["Title"].ToString();

                            // ImageIndex (int) 처리: NULL이면 0으로 간주
                            int imageIndex = reader.IsDBNull(reader.GetOrdinal("ImageIndex")) ?
                                             0 : Convert.ToInt32(reader["ImageIndex"]);

                            // IsVisible (bool) 처리: NULL이면 false로 간주
                            bool isVisible = reader.IsDBNull(reader.GetOrdinal("IsVisible")) ?
                                             false : Convert.ToBoolean(reader["IsVisible"]);

                            // IsLocked (bool) 처리: NULL이면 false로 간주
                            bool isLocked = reader.IsDBNull(reader.GetOrdinal("IsLocked")) ?
                                             false : Convert.ToBoolean(reader["IsLocked"]);

                            racks.Add(new Rack
                            {
                                Id = id,
                                Title = title,
                                ImageIndex = imageIndex,
                                IsVisible = isVisible,
                                IsLocked = isLocked
                            });
                        }
                    }
                }
            }
            return racks;
        }

        // 랙 상태 업데이트 메서드 (필요시)
        public async Task UpdateRackStateAsync(string rackId, int newImageIndex)
        {
            string query = "UPDATE RackState SET rack_type = @RackType, bullet_type = @BulletType WHERE id = @Id";

            // newImageIndex를 rack_type과 bullet_type으로 분해
            // rack_type*3 + bullet_type
            // rack_type = ImageIndex / 3 (정수 나눗셈)
            // bullet_type = ImageIndex % 3
            int rackType = newImageIndex / 3;
            int bulletType = newImageIndex % 3;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@RackType", rackType);
                    command.Parameters.AddWithValue("@BulletType", bulletType);
                    command.Parameters.AddWithValue("@Id", rackId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}