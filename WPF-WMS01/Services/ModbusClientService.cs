using Modbus.Device; // NModbus4 라이브러리
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.IO.Ports; // 시리얼 포트 통신을 위해 사용
using System.Diagnostics; // Debug.WriteLine을 위해 추가

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
        private string _ipAddress; // PLC의 IP 주소 (TCP용)
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
            Debug.WriteLine($"[ModbusService] Initialized for TCP: {ipAddress}:{port}, Slave ID: {slaveId}");
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
            Debug.WriteLine($"[ModbusService] Initialized for RTU: {comPortName}, {baudRate}bps, Slave ID: {slaveId}");
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
        /// Modbus PLC에 비동기적으로 연결을 시도합니다. (현재 설정된 모드에 따라 TCP 또는 RTU)
        /// Attempts to connect to the Modbus PLC asynchronously (TCP or RTU depending on the current mode).
        /// </summary>
        public async Task ConnectAsync() // 메서드 이름을 ConnectAsync로 변경하고 Task 반환
        {
            Dispose(); // 기존 연결이 있다면 해제
            try
            {
                if (_currentMode == ModbusMode.TCP)
                {
                    Debug.WriteLine($"[ModbusService] Attempting to connect via TCP to {_ipAddress}:{_port}...");
                    _tcpClient = new TcpClient();
                    // ConnectAsync를 사용하여 비동기적으로 연결 시도
                    await _tcpClient.ConnectAsync(_ipAddress, _port).ConfigureAwait(false);

                    if (_tcpClient.Connected)
                    {
                        _master = ModbusIpMaster.CreateIp(_tcpClient); // Modbus TCP 마스터 생성
                        Debug.WriteLine($"[ModbusService] Modbus TCP Connected to {_ipAddress}:{_port}");
                    }
                    else
                    {
                        Debug.WriteLine($"[ModbusService] Failed to connect to {_ipAddress}:{_port}. TcpClient not connected.");
                    }
                }
                else // ModbusMode.RTU
                {
                    Debug.WriteLine($"[ModbusService] Attempting to connect via RTU to {_comPortName} at {_baudRate} baud...");
                    _serialPort = new SerialPort(_comPortName, _baudRate, _parity, _dataBits, _stopBits);
                    // SerialPort.Open()은 동기 메서드이므로 Task.Run을 사용하여 백그라운드 스레드에서 실행
                    await Task.Run(() => _serialPort.Open()).ConfigureAwait(false);

                    if (_serialPort.IsOpen)
                    {
                        _master = ModbusSerialMaster.CreateRtu(_serialPort); // Modbus RTU 마스터 생성
                        Debug.WriteLine($"[ModbusService] Modbus RTU Connected to {_comPortName} at {_baudRate} baud.");
                    }
                    else
                    {
                        Debug.WriteLine($"[ModbusService] Failed to open serial port {_comPortName}. SerialPort not open.");
                    }
                }

                if (_master != null)
                {
                    _master.Transport.ReadTimeout = 2000; // 응답 대기 시간 (ms)
                    _master.Transport.WriteTimeout = 2000;
                    Debug.WriteLine("[ModbusService] Modbus master transport timeouts set.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Connection Error: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
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
                Debug.WriteLine("[ModbusService] Read request: Not Connected. Attempting to reconnect...");
                await ConnectAsync().ConfigureAwait(false); // ConnectAsync 호출
                if (!IsConnected)
                {
                    Debug.WriteLine("[ModbusService] Read request: Reconnection failed. Cannot read coils.");
                    return null; // 재연결 실패 시 null 반환
                }
            }

            try
            {
                // ReadCoils: 코일(Coils) 값을 읽어옵니다. (기능 코드 0x01)
                // ConfigureAwait(false)를 사용하여 호출 스레드로 돌아가지 않도록 함
                bool[] coils = await _master.ReadCoilsAsync(_slaveId, startAddress, numberOfPoints).ConfigureAwait(false);
                // Debug.WriteLine($"[ModbusService] Read coils from {startAddress}, count {numberOfPoints}. Data: [{string.Join(", ", coils.Select(c => c ? "1" : "0"))}]");
                return coils;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading coils from {startAddress}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                return null;
            }
        }

        /// <summary>
        /// 지정된 단일 코일(Coil)의 상태를 비동기적으로 읽어옵니다.
        /// Asynchronously reads the state of a single coil at the specified address.
        /// </summary>
        /// <param name="address">읽을 코일 주소</param>
        /// <returns>코일 값 (bool), 오류 발생 시 false</returns>
        public async Task<bool> ReadSingleCoilAsync(ushort address)
        {
            if (!IsConnected)
            {
                Debug.WriteLine($"[ModbusService] Read single coil request: Not Connected. Attempting to reconnect for address {address}...");
                await ConnectAsync().ConfigureAwait(false);
                if (!IsConnected)
                {
                    Debug.WriteLine($"[ModbusService] Read single coil request: Reconnection failed. Cannot read coil {address}.");
                    return false;
                }
            }

            try
            {
                bool[] coils = await _master.ReadCoilsAsync(_slaveId, address, 1).ConfigureAwait(false);
                // Debug.WriteLine($"[ModbusService] Read single coil at address {address}. Value: {(coils != null && coils.Length > 0 ? (coils[0] ? "1" : "0") : "N/A")}");
                return coils != null && coils.Length > 0 && coils[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading single coil at address {address}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose();
                return false;
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
                Debug.WriteLine($"[ModbusService] Write request for address {address}: Not Connected. Attempting to reconnect...");
                await ConnectAsync().ConfigureAwait(false); // ConnectAsync 호출
                if (!IsConnected)
                {
                    Debug.WriteLine($"[ModbusService] Write request for address {address}: Reconnection failed. Cannot write coil.");
                    return false; // 재연결 실패 시 false 반환
                }
            }

            try
            {
                // WriteSingleCoil: 단일 코일(Coil) 값을 씁니다. (기능 코드 0x05)
                // ConfigureAwait(false)를 사용하여 호출 스레드로 돌아가지 않도록 함
                await _master.WriteSingleCoilAsync(_slaveId, address, value).ConfigureAwait(false);
                Debug.WriteLine($"[ModbusService] Coil at address {address} set to {value}. Write successful.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error writing coil to address {address}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
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
            Debug.WriteLine("[ModbusService] Disposing Modbus resources...");
            try
            {
                // Dispose Modbus master first
                if (_master != null)
                {
                    _master.Dispose();
                    Debug.WriteLine("[ModbusService] Modbus master disposed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error disposing Modbus master: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _master = null;
            }

            try
            {
                // Dispose TcpClient
                if (_tcpClient != null)
                {
                    if (_tcpClient.Connected)
                    {
                        _tcpClient.Close();
                        Debug.WriteLine("[ModbusService] TcpClient closed.");
                    }
                    _tcpClient.Dispose();
                    Debug.WriteLine("[ModbusService] TcpClient disposed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error disposing TcpClient: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _tcpClient = null;
            }

            try
            {
                // Dispose SerialPort
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                        Debug.WriteLine("[ModbusService] SerialPort closed.");
                    }
                    _serialPort.Dispose();
                    Debug.WriteLine("[ModbusService] SerialPort disposed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error disposing SerialPort: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _serialPort = null;
            }
            Debug.WriteLine("[ModbusService] Modbus Disconnected and all resources released.");
        }
    }
}
