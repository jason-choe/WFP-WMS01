// Services/ModbusClientService.cs
using Modbus.Device; // NModbus4 라이브러리
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.IO.Ports; // 시리얼 포트 통신을 위해 사용
using System.Diagnostics; // Debug.WriteLine을 위해 추가
using System.Linq; // for .Select() in logging
using System.Threading; // CancellationTokenSource 추가

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
        private CancellationTokenSource _cancellationTokenSource; // 연결 취소 토큰 소스

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
            if (IsConnected)
            {
                Debug.WriteLine("[ModbusService] Already connected. Skipping new connection attempt.");
                return;
            }

            Dispose(); // 기존 연결이 있다면 해제

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancellationTokenSource.Token;

                if (_currentMode == ModbusMode.TCP)
                {
                    Debug.WriteLine($"[ModbusService] Attempting to connect via TCP to {_ipAddress}:{_port}...");
                    _tcpClient = new TcpClient();

                    // 연결 타임아웃을 설정 (예: 5초)
                    var connectTask = _tcpClient.ConnectAsync(_ipAddress, _port);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), token);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                    if (completedTask == timeoutTask)
                    {
                        token.ThrowIfCancellationRequested(); // TimeoutException 발생
                    }

                    if (_tcpClient.Connected)
                    {
                        _master = ModbusIpMaster.CreateIp(_tcpClient); // Modbus TCP 마스터 생성
                        Debug.WriteLine($"[ModbusService] Modbus TCP Connected to {_ipAddress}:{_port}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to connect to {_ipAddress}:{_port}. TcpClient not connected.");
                    }
                }
                else // ModbusMode.RTU
                {
                    Debug.WriteLine($"[ModbusService] Attempting to connect via RTU to {_comPortName} at {_baudRate} baud...");
                    _serialPort = new SerialPort(_comPortName, _baudRate, _parity, _dataBits, _stopBits);

                    // SerialPort.Open()은 동기 메서드이므로 Task.Run을 사용하여 백그라운드 스레드에서 실행
                    await Task.Run(() => _serialPort.Open(), token).ConfigureAwait(false);

                    if (_serialPort.IsOpen)
                    {
                        _master = ModbusSerialMaster.CreateRtu(_serialPort); // Modbus RTU 마스터 생성
                        Debug.WriteLine($"[ModbusService] Modbus RTU Connected to {_comPortName} at {_baudRate} baud.");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to open serial port {_comPortName}. SerialPort not open.");
                    }
                }

                if (_master != null)
                {
                    _master.Transport.ReadTimeout = 2000; // 응답 대기 시간 (ms)
                    _master.Transport.WriteTimeout = 2000;
                    Debug.WriteLine("[ModbusService] Modbus master transport timeouts set.");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[ModbusService] Connection attempt to {_ipAddress ?? _comPortName} was cancelled (timeout).");
                Dispose(); // 자원 정리
                throw new InvalidOperationException($"Modbus connection to {_ipAddress ?? _comPortName} was cancelled (timeout).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Connection Error: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose(); // 오류 발생 시 자원 해제
                throw; // 예외 다시 던지기
            }
        }

        /// <summary>
        /// Modbus PLC와의 연결을 해제합니다.
        /// Disconnects from the Modbus PLC.
        /// </summary>
        public void Disconnect()
        {
            Dispose(); // Dispose 메서드를 호출하여 자원 해제 로직을 재사용
        }

        /// <summary>
        /// 지정된 시작 주소부터 Discrete Input 상태들을 비동기적으로 읽어옵니다. (Call Button 입력용)
        /// Asynchronously reads Discrete Input states from a specified start address. (For Call Button input)
        /// </summary>
        /// <param name="startAddress">읽기 시작할 Discrete Input 주소</param>
        /// <param name="numberOfPoints">읽을 Discrete Input의 개수</param>
        /// <returns>읽은 Discrete Input 값들의 배열 (bool[]), 오류 발생 시 null</returns>
        /// <exception cref="InvalidOperationException">Modbus 서비스가 연결되지 않은 경우 발생.</exception>
        public async Task<bool[]> ReadDiscreteInputStatesAsync(ushort startAddress, ushort numberOfPoints)
        {
            if (!IsConnected || _master == null)
            {
                Debug.WriteLine($"[ModbusService] ReadDiscreteInputStatesAsync: Not connected or master is null. Cannot read.");
                // 연결이 안 된 상태에서 읽기 시도 시 명시적으로 예외 발생
                throw new InvalidOperationException("Modbus service is not connected. Cannot read discrete input states.");
            }

            try
            {
                // ReadInputs: Discrete Inputs 값을 읽어옵니다. (기능 코드 0x02)
                bool[] inputs = await _master.ReadInputsAsync(_slaveId, startAddress, numberOfPoints).ConfigureAwait(false);
                // Debug.WriteLine($"[ModbusService] Read discrete inputs from {startAddress}, count {numberOfPoints}. Data: [{string.Join(", ", inputs.Select(c => c ? "1" : "0"))}]");
                return inputs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading discrete inputs from {startAddress}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose(); // 통신 오류 발생 시 연결 해제
                throw; // 예외 다시 던지기
            }
        }

        /// <summary>
        /// 지정된 단일 코일(Coil)의 상태를 비동기적으로 읽어옵니다. (경광등 상태 확인용)
        /// Asynchronously reads the state of a single coil at the specified address. (For indicator lamp status)
        /// </summary>
        /// <param name="address">읽을 코일 주소</param>
        /// <returns>코일 값 (bool), 오류 발생 시 false</returns>
        /// <exception cref="InvalidOperationException">Modbus 서비스가 연결되지 않은 경우 발생.</exception>
        public async Task<bool> ReadSingleCoilAsync(ushort address)
        {
            if (!IsConnected || _master == null)
            {
                Debug.WriteLine($"[ModbusService] Read single coil request: Not Connected. Cannot read.");
                // 연결이 안 된 상태에서 읽기 시도 시 명시적으로 예외 발생
                throw new InvalidOperationException("Modbus service is not connected. Cannot read single coil.");
            }

            try
            {
                bool[] coils = await _master.ReadCoilsAsync(_slaveId, address, 1).ConfigureAwait(false);
                return coils != null && coils.Length > 0 && coils[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading single coil at address {address}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose();
                throw; // 예외 다시 던지기
            }
        }


        /// <summary>
        /// 지정된 주소의 단일 코일(Coil) 값을 비동기적으로 씁니다. (경광등 제어용)
        /// Asynchronously writes a single coil value to a specified address (for controlling indicator lamp).
        /// </summary>
        /// <param name="address">쓸 코일의 주소</param>
        /// <param name="value">설정할 값 (true/false)</param>
        /// <returns>쓰기 작업 성공 여부</returns>
        /// <exception cref="InvalidOperationException">Modbus 서비스가 연결되지 않은 경우 발생.</exception>
        public async Task<bool> WriteSingleCoilAsync(ushort address, bool value)
        {
            if (!IsConnected || _master == null)
            {
                Debug.WriteLine($"[ModbusService] Write request for address {address}: Not Connected. Cannot write.");
                // 연결이 안 된 상태에서 쓰기 시도 시 명시적으로 예외 발생
                throw new InvalidOperationException("Modbus service is not connected. Cannot write single coil.");
            }

            try
            {
                // WriteSingleCoil: 단일 코일(Coil) 값을 씁니다. (기능 코드 0x05)
                await _master.WriteSingleCoilAsync(_slaveId, address, value).ConfigureAwait(false);
                Debug.WriteLine($"[ModbusService] Coil at address {address} set to {value}. Write successful.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error writing coil to address {address}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                throw; // 예외 다시 던지기
            }
        }

        /// <summary>
        /// Modbus Holding Register의 값을 비동기적으로 읽습니다.
        /// </summary>
        /// <param name="startAddress">시작 Holding Register 주소.</param>
        /// <param name="numberOfPoints">읽을 Holding Register 개수.</param>
        /// <returns>Holding Register 값 배열.</returns>
        /// <exception cref="InvalidOperationException">Modbus 서비스가 연결되지 않은 경우 발생.</exception>
        public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort numberOfPoints)
        {
            if (!IsConnected || _master == null)
            {
                Debug.WriteLine($"[ModbusService] ReadHoldingRegistersAsync: Not connected or master is null. Cannot read.");
                throw new InvalidOperationException("Modbus service is not connected. Cannot read holding registers.");
            }
            try
            {
                ushort[] registers = await _master.ReadHoldingRegistersAsync(_slaveId, startAddress, numberOfPoints).ConfigureAwait(false);
                return registers;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading holding registers from {startAddress}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                throw;
            }
        }

        /// <summary>
        /// Modbus Holding Register에 단일 값을 비동기적으로 씁니다.
        /// </summary>
        /// <param name="address">Holding Register 주소.</param>
        /// <param name="value">설정할 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        /// <exception cref="InvalidOperationException">Modbus 서비스가 연결되지 않은 경우 발생.</exception>
        public async Task<bool> WriteSingleHoldingRegisterAsync(ushort address, ushort value)
        {
            if (!IsConnected || _master == null)
            {
                Debug.WriteLine($"[ModbusService] WriteSingleHoldingRegisterAsync: Not connected or master is null. Cannot write.");
                throw new InvalidOperationException("Modbus service is not connected. Cannot write single holding register.");
            }

            try
            {
                await _master.WriteSingleRegisterAsync(_slaveId, address, value).ConfigureAwait(false);
                Debug.WriteLine($"[ModbusService] Register at address {address} set to {value}. Write successful.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error writing holding register {address}: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                throw;
            }
        }

        /// <summary>
        /// 자원을 해제합니다.
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            Debug.WriteLine("[ModbusService] Disposing Modbus resources...");
            // CancellationTokenSource 먼저 취소 및 해제
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error disposing CancellationTokenSource: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource = null;
            }

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
                _master = null; // null로 명확히 설정
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
                _tcpClient = null; // null로 명확히 설정
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
