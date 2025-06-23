using Modbus.Device; // NModbus4 라이브러리
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace WPF_WMS01.Services
{
    /// <summary>
    /// Modbus TCP 통신을 담당하는 서비스 클래스입니다.
    /// PLC 연결 및 코일(Coil) 상태 읽기/쓰기 기능을 제공합니다.
    /// </summary>
    public class ModbusClientService : IDisposable
    {
        private TcpClient _tcpClient; // TCP 클라이언트 인스턴스
        private ModbusIpMaster _master; // Modbus 마스터 인스턴스
        private string _ipAddress; // PLC의 IP 주소
        private int _port; // Modbus TCP 포트 (일반적으로 502)
        private byte _slaveId; // Modbus 슬레이브 ID (Unit ID)

        /// <summary>
        /// ModbusClientService의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="ipAddress">PLC의 IP 주소</param>
        /// <param name="port">Modbus TCP 포트</param>
        /// <param name="slaveId">Modbus 슬레이브 ID</param>
        public ModbusClientService(string ipAddress, int port, byte slaveId)
        {
            _ipAddress = ipAddress;
            _port = port;
            _slaveId = slaveId;
        }

        /// <summary>
        /// 현재 Modbus가 연결되어 있는지 여부를 나타냅니다.
        /// </summary>
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        /// <summary>
        /// Modbus PLC에 연결을 시도합니다.
        /// </summary>
        public void Connect()
        {
            try
            {
                // 기존 연결이 있다면 닫고 초기화
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }

                _tcpClient = new TcpClient();
                _tcpClient.Connect(_ipAddress, _port); // PLC에 연결 시도

                if (_tcpClient.Connected)
                {
                    _master = ModbusIpMaster.CreateIp(_tcpClient); // Modbus 마스터 생성
                    _master.Transport.ReadTimeout = 2000; // 응답 대기 시간 (ms)
                    _master.Transport.WriteTimeout = 2000;
                    Console.WriteLine($"[ModbusService] Modbus Connected to {_ipAddress}:{_port}");
                }
                else
                {
                    Console.WriteLine($"[ModbusService] Failed to connect to {_ipAddress}:{_port}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModbusService] Connection Error: {ex.Message}");
                Dispose(); // 오류 발생 시 자원 해제
            }
        }

        /// <summary>
        /// Modbus PLC와의 연결을 해제합니다.
        /// </summary>
        public void Disconnect()
        {
            Dispose();
            Console.WriteLine("[ModbusService] Modbus Disconnected.");
        }

        /// <summary>
        /// 지정된 시작 주소부터 콜(Coil) 상태들을 비동기적으로 읽어옵니다.
        /// </summary>
        /// <param name="startAddress">읽기 시작할 코일 주소</param>
        /// <param name="numberOfPoints">읽을 코일의 개수</param>
        /// <returns>읽은 코일 값들의 배열 (bool[]), 오류 발생 시 null</returns>
        public async Task<bool[]> ReadCallButtonStatesAsync(ushort startAddress, ushort numberOfPoints)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ModbusService] Not Connected. Attempting to reconnect...");
                Connect(); // 연결 끊겼으면 재연결 시도
                if (!IsConnected) return null; // 재연결 실패 시 null 반환
            }

            try
            {
                // ReadCoils: 코일(Coils) 값을 읽어옵니다. (기능 코드 0x01)
                bool[] coils = await _master.ReadCoilsAsync(_slaveId, startAddress, numberOfPoints);
                return coils;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModbusService] Error reading coils from {startAddress}: {ex.Message}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                return null;
            }
        }

        /// <summary>
        /// 지정된 주소의 단일 콜(Coil) 값을 비동기적으로 씁니다. (콜 취소 등에 사용)
        /// </summary>
        /// <param name="address">쓸 코일의 주소</param>
        /// <param name="value">설정할 값 (true/false)</param>
        /// <returns>쓰기 작업 성공 여부</returns>
        public async Task<bool> WriteSingleCoilAsync(ushort address, bool value)
        {
            if (!IsConnected)
            {
                Console.WriteLine("[ModbusService] Not Connected. Attempting to reconnect...");
                Connect(); // 연결 끊겼으면 재연결 시도
                if (!IsConnected) return false; // 재연결 실패 시 false 반환
            }

            try
            {
                // WriteSingleCoil: 단일 코일(Coil) 값을 씁니다. (기능 코드 0x05)
                await _master.WriteSingleCoilAsync(_slaveId, address, value);
                Console.WriteLine($"[ModbusService] Coil at address {address} set to {value}.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModbusService] Error writing coil to address {address}: {ex.Message}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                return false;
            }
        }

        /// <summary>
        /// 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            _master?.Dispose(); // Modbus 마스터 해제
            _master = null;

            if (_tcpClient != null)
            {
                _tcpClient.Close(); // TCP 클라이언트 닫기
                _tcpClient.Dispose(); // TCP 클라이언트 자원 해제
                _tcpClient = null;
            }
        }
    }
}
