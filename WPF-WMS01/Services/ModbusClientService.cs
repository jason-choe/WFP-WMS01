using Modbus.Device; // NModbus4 라이브러리
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;

namespace WPF_WMS01.Services
{
    /// <summary>
    /// Modbus 통신 모드를 정의합니다.
    /// Defines Modbus communication modes.
    /// </summary>
    public enum ModbusMode
    {
        TCP,
        RTU
    }

    /// <summary>
    /// Modbus TCP 및 RTU 통신을 담당하는 서비스 클래스입니다.
    /// PLC 연결 및 코일(Coil) 상태 읽기/쓰기 기능을 제공합니다.
    /// Service class responsible for Modbus TCP and RTU communication.
    /// Provides PLC connection and coil (Coil) state read/write functionalities.
    /// </summary>
    public class ModbusClientService : IDisposable
    {
        private TcpClient _tcpClient; // TCP 클라이언트 인스턴스
        private SerialPort _serialPort; // RTU 시리얼 포트 인스턴스
        private IModbusMaster _master; // Modbus 마스터 인스턴스 (TCP 또는 RTU)
        private ModbusMode _currentMode; // 현재 Modbus 통신 모드
        private string _ipAddress; // PLC의 IP 주소
        private int _port; // Modbus TCP 포트 (일반적으로 502)
        private string _comPortName; // 시리얼 포트 이름 (RTU용)
        private int _baudRate; // 보드 레이트 (RTU용)
        private Parity _parity; // 패리티 (RTU용)
        private StopBits _stopBits; // 스톱 비트 (RTU용)
        private int _dataBits; // 데이터 비트 (RTU용)
        private byte _slaveId; // Modbus 슬레이브 ID (Unit ID)

        /// <summary>
        /// ModbusClientService의 새 인스턴스를 TCP 모드로 초기화합니다.
        /// Initializes a new instance of ModbusClientService for TCP mode.
        /// </summary>
        /// <param name="ipAddress">PLC의 IP 주소</param>
        /// <param name="port">Modbus TCP 포트</param>
        /// <param name="slaveId">Modbus 슬레이브 ID</param>
        public ModbusClientService(string ipAddress, int port, byte slaveId)
        {
            _currentMode = ModbusMode.TCP;
            _ipAddress = ipAddress;
            _port = port;
            _slaveId = slaveId;
        }

        /// <summary>
        /// ModbusClientService의 새 인스턴스를 RTU 모드로 초기화합니다.
        /// Initializes a new instance of ModbusClientService for RTU mode.
        /// </summary>
        /// <param name="comPortName">시리얼 포트 이름 (예: "COM1")</param>
        /// <param name="baudRate">보드 레이트</param>
        /// <param name="parity">패리티</param>
        /// <param name="stopBits">스톱 비트</param>
        /// <param name="dataBits">데이터 비트</param>
        /// <param name="slaveId">Modbus 슬레이브 ID</param>
        public ModbusClientService(string comPortName, int baudRate, Parity parity, StopBits stopBits, int dataBits, byte slaveId)
        {
            _currentMode = ModbusMode.RTU;
            _comPortName = comPortName;
            _baudRate = baudRate;
            _parity = parity;
            _stopBits = stopBits;
            _dataBits = dataBits;
            _slaveId = slaveId;
        }

        /// <summary>
        /// 현재 Modbus가 연결되어 있는지 여부를 나타냅니다.
        /// Indicates whether Modbus is currently connected.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_master == null) return false;

                if (_currentMode == ModbusMode.TCP)
                {
                    return _tcpClient != null && _tcpClient.Connected;
                }
                else // ModbusMode.RTU
                {
                    return _serialPort != null && _serialPort.IsOpen;
                }
            }
        }

        /// <summary>
        /// Modbus PLC에 연결을 시도합니다. (현재 설정된 모드에 따라 TCP 또는 RTU)
        /// Attempts to connect to the Modbus PLC (TCP or RTU depending on the current mode).
        /// </summary>
        public void Connect()
        {
            Dispose(); // 기존 연결이 있다면 해제
            try
            {
                if (_currentMode == ModbusMode.TCP)
                {
                    _tcpClient = new TcpClient();
                    _tcpClient.Connect(_ipAddress, _port); // PLC에 연결 시도

                    if (_tcpClient.Connected)
                    {
                        _master = ModbusIpMaster.CreateIp(_tcpClient); // Modbus TCP 마스터 생성
                        Console.WriteLine($"[ModbusService] Modbus TCP Connected to {_ipAddress}:{_port}");
                    }
                    else
                    {
                        Console.WriteLine($"[ModbusService] Failed to connect to {_ipAddress}:{_port}.");
                    }
                }
                else // ModbusMode.RTU
                {
                    _serialPort = new SerialPort(_comPortName, _baudRate, _parity, _dataBits, _stopBits);
                    _serialPort.Open(); // 시리얼 포트 열기

                    if (_serialPort.IsOpen)
                    {
                        _master = ModbusSerialMaster.CreateRtu(_serialPort); // Modbus RTU 마스터 생성
                        Console.WriteLine($"[ModbusService] Modbus RTU Connected to {_comPortName} at {_baudRate} baud.");
                    }
                    else
                    {
                        Console.WriteLine($"[ModbusService] Failed to open serial port {_comPortName}.");
                    }
                }

                if (_master != null)
                {
                    _master.Transport.ReadTimeout = 2000; // 응답 대기 시간 (ms)
                    _master.Transport.WriteTimeout = 2000;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Connection Error: {ex.Message}");
                Dispose(); // 오류 발생 시 자원 해제
            }
        }

        /// <summary>
        /// Modbus PLC와의 연결을 해제합니다.
        /// Disconnects from the Modbus PLC.
        /// </summary>
        public void Disconnect()
        {
            Dispose();
            Debug.WriteLine("[ModbusService] Modbus Disconnected.");
        }

        /// <summary>
        /// 지정된 시작 주소부터 코일(Coil) 상태들을 비동기적으로 읽어옵니다.
        /// Asynchronously reads coil states from a specified start address.
        /// </summary>
        /// <param name="startAddress">읽기 시작할 코일 주소</param>
        /// <param name="numberOfPoints">읽을 코일의 개수</param>
        /// <returns>읽은 코일 값들의 배열 (bool[]), 오류 발생 시 null</returns>
        public async Task<bool[]> ReadCallButtonStatesAsync(ushort startAddress, ushort numberOfPoints)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[ModbusService] Not Connected. Attempting to reconnect...");
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
        /// 지정된 주소의 단일 코일(Coil) 값을 비동기적으로 씁니다. (코일 취소 등에 사용)
        /// Asynchronously writes a single coil value to a specified address (used for coil cancellation, etc.).
        /// </summary>
        /// <param name="address">쓸 코일의 주소</param>
        /// <param name="value">설정할 값 (true/false)</param>
        /// <returns>쓰기 작업 성공 여부</returns>
        public async Task<bool> WriteSingleCoilAsync(ushort address, bool value)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[ModbusService] Not Connected. Attempting to reconnect...");
                Connect(); // 연결 끊겼으면 재연결 시도
                if (!IsConnected) return false; // 재연결 실패 시 false 반환
            }

            try
            {
                // WriteSingleCoil: 단일 코일(Coil) 값을 씁니다. (기능 코드 0x05)
                await _master.WriteSingleCoilAsync(_slaveId, address, value);
                Debug.WriteLine($"[ModbusService] Coil at address {address} set to {value}.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error writing coil to address {address}: {ex.Message}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                return false;
            }
        }

        /// <summary>
        /// 자원을 해제합니다.
        /// Releases resources.
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
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close(); // 시리얼 포트 닫기
                _serialPort.Dispose(); // 시리얼 포트 자원 해제
                _serialPort = null;
            }
        }
    }
}
// Modbus/TCP를 사용하여 192.168.1.10 IP 주소와 502 포트, 슬레이브 ID 1로 연결
//     ModbusClientService _modbusTcpService = new ModbusClientService("192.168.1.10", 502, 1);
//    _modbusTcpService.Connect();
// 이제 _modbusTcpService를 통해 ReadCallButtonStatesAsync, WriteSingleCoilAsync 등을 호출할 수 있습니다.
// Modbus/RTU를 사용하여 COM1 포트, 9600 보드 레이트, None 패리티, One 스톱 비트, 8 데이터 비트, 슬레이브 ID 1로 연결
//    ModbusClientService _modbusRtuService = new ModbusClientService("COM1", 9600, Parity.None, StopBits.One, 8, 1);
//    _modbusRtuService.Connect();
// 이제 _modbusRtuService를 통해 ReadCallButtonStatesAsync, WriteSingleCoilAsync 등을 호출할 수 있습니다.