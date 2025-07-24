using System;
using System.IO.Ports; // SerialPort, Parity, StopBits를 위해 필요
using System.Net.Sockets; // TcpClient를 위해 필요
using System.Threading.Tasks;
using Modbus.Device; // Modbus.Device 라이브러리 사용
using System.Diagnostics; // Debug.WriteLine을 위해 필요

namespace WPF_WMS01.Services
{
    /// <summary>
    /// Modbus TCP 또는 RTU 통신을 위한 클라이언트 서비스입니다.
    /// 이 서비스는 Modbus 마스터 역할을 수행하여 PLC와 통신합니다.
    /// </summary>
    public class ModbusClientService : IDisposable
    {
        private TcpClient _tcpClient;
        private ModbusMaster _modbusMaster;
        private SerialPort _serialPort; // RTU 모드를 위한 시리얼 포트
        private readonly string _modbusMode; // "TCP" 또는 "RTU"
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly byte _slaveId;
        private readonly Guid _instanceId = Guid.NewGuid(); // 인스턴스별 고유 ID 추가

        // RTU 모드를 위한 추가 필드
        private readonly string _comPort;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly StopBits _stopBits;
        private readonly int _dataBits;

        private bool _isConnected;
        public bool IsConnected => _isConnected;

        // 내부 연결 재시도 상수
        private const int MAX_INTERNAL_CONNECT_RETRIES = 2; // ConnectAsync 내부에서 최대 2회 재시도 (총 3번 시도)
        private const int INTERNAL_RETRY_DELAY_MS = 500; // 내부 재시도 간 500ms 지연

        /// <summary>
        /// Modbus TCP 클라이언트 서비스를 초기화합니다.
        /// </summary>
        /// <param name="ipAddress">Modbus TCP 서버의 IP 주소.</param>
        /// <param name="port">Modbus TCP 서버의 포트 번호.</param>
        /// <param name="slaveId">통신할 Modbus 슬레이브 ID.</param>
        public ModbusClientService(string ipAddress, int port, byte slaveId)
        {
            _modbusMode = "TCP";
            _ipAddress = ipAddress;
            _port = port;
            _slaveId = slaveId;
            _isConnected = false;
            Debug.WriteLine($"[ModbusClientService] Initialized for TCP: {_ipAddress}:{_port}, Slave ID: {_slaveId}. Instance ID: {_instanceId}");
        }

        /// <summary>
        /// Modbus RTU 클라이언트 서비스를 초기화합니다.
        /// </summary>
        /// <param name="comPort">시리얼 포트 이름 (예: "COM1").</param>
        /// <param name="baudRate">전송 속도 (예: 9600).</param>
        /// <param name="parity">패리티 비트 설정.</param>
        /// <param name="stopBits">정지 비트 설정.</param>
        /// <param name="dataBits">데이터 비트 설정.</param>
        /// <param name="slaveId">통신할 Modbus 슬레이브 ID.</param>
        public ModbusClientService(string comPort, int baudRate, Parity parity, StopBits stopBits, int dataBits, byte slaveId)
        {
            _modbusMode = "RTU";
            _comPort = comPort;
            _baudRate = baudRate;
            _parity = parity;
            _stopBits = stopBits;
            _dataBits = dataBits;
            _slaveId = slaveId;
            _isConnected = false;
            Debug.WriteLine($"[ModbusClientService] Initialized for RTU: COM Port: {_comPort}, Baud: {_baudRate}, Slave ID: {_slaveId}. Instance ID: {_instanceId}");
        }

        /// <summary>
        /// Modbus 서버에 비동기적으로 연결합니다.
        /// </summary>
        /// <returns>연결 성공 여부.</returns>
        public async Task<bool> ConnectAsync()
        {
            if (_isConnected)
            {
                Debug.WriteLine($"[ModbusClientService] Already connected. Instance ID: {_instanceId}");
                return true;
            }

            Dispose(); // 기존 연결이 있다면 정리

            try
            {
                if (_modbusMode == "TCP")
                {
                    int internalConnectAttempt = 0;
                    while (internalConnectAttempt <= MAX_INTERNAL_CONNECT_RETRIES)
                    {
                        try
                        {
                            _tcpClient = new TcpClient(); // 각 시도마다 새로운 TcpClient 인스턴스 생성
                            Debug.WriteLine($"[ModbusClientService] Attempting to connect to TCP: {_ipAddress}:{_port}... (Internal attempt {internalConnectAttempt + 1}). Instance ID: {_instanceId}");

                            // 연결 시도에 타임아웃을 적용
                            var connectTask = _tcpClient?.ConnectAsync(_ipAddress, _port);
                            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5)); // 5초 타임아웃

                            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                            if (completedTask == timeoutTask)
                            {
                                Debug.WriteLine($"[ModbusClientService] TCP connection timed out during internal retry. Instance ID: {_instanceId}");
                                throw new TimeoutException("Modbus TCP 연결 시간이 초과되었습니다.");
                            }

                            if (!(_tcpClient?.Connected ?? false)) // != null && !_tcpClient.Connected)
                            {
                                Debug.WriteLine($"[ModbusClientService] TCP connection failed during internal retry (TcpClient.Connected is false). Instance ID: {_instanceId}");
                                throw new Exception("Modbus TCP 연결에 실패했습니다.");
                            }

                            // 연결 성공 후 잠시 대기하여 소켓 안정화
                            await Task.Delay(100); // 100ms 대기

                            // ModbusMaster 생성 및 Transport 속성 설정 로직을 별도의 try-catch로 감싸서 더 명확하게 오류 처리
                            try
                            {
                                _modbusMaster = Modbus.Device.ModbusIpMaster.CreateIp(_tcpClient);

                                // ModbusMaster 생성 후 null 체크 추가
                                if (_modbusMaster == null)
                                {
                                    Debug.WriteLine($"[ModbusClientService] ModbusIpMaster.CreateIp returned null unexpectedly. Instance ID: {_instanceId}");
                                    throw new InvalidOperationException("Failed to create Modbus Master instance: CreateIp returned null.");
                                }

                                // _modbusMaster.Transport가 null인지 추가 확인 (NRE 방지)
                                if (_modbusMaster.Transport == null)
                                {
                                    Debug.WriteLine($"[ModbusClientService] ModbusMaster.Transport is null after creation. This indicates an issue with ModbusIpMaster.CreateIp. Instance ID: {_instanceId}");
                                    throw new InvalidOperationException("Failed to initialize Modbus Master: Transport property is null.");
                                }

                                _modbusMaster.Transport.ReadTimeout = 1000; // 읽기 타임아웃 설정
                                _modbusMaster.Transport.WriteTimeout = 1000; // 쓰기 타임아웃 설정
                                _isConnected = true;
                                Debug.WriteLine($"[ModbusClientService] TCP connection established and master created. Instance ID: {_instanceId}");
                                return true; // 성공 시 즉시 반환
                            }
                            catch (Exception masterConfigEx)
                            {
                                Debug.WriteLine($"[ModbusClientService] Error during Modbus Master creation or configuration: {masterConfigEx.Message}. Instance ID: {_instanceId}");
                                // ModbusMaster 생성/설정 중 오류 발생 시, 이를 연결 실패로 간주하고 재시도
                                throw new Exception($"Modbus Master 설정 중 오류 발생: {masterConfigEx.Message}", masterConfigEx);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ModbusClientService] Internal connect attempt {internalConnectAttempt + 1} failed: {ex.Message}. Instance ID: {_instanceId}");

                            // 실패 시 _modbusMaster도 확실히 null로 설정
                            _modbusMaster = null;

                            _tcpClient?.Close(); // 실패한 클라이언트 정리
                            _tcpClient?.Dispose();
                            _tcpClient = null;

                            internalConnectAttempt++;
                            if (internalConnectAttempt <= MAX_INTERNAL_CONNECT_RETRIES)
                            {
                                // 다음 내부 재시도 전 지연
                                await Task.Delay(TimeSpan.FromMilliseconds(INTERNAL_RETRY_DELAY_MS));
                            }
                            else
                            {
                                // 최대 내부 재시도 횟수 도달, 마지막 예외를 상위 호출자로 다시 던짐
                                _isConnected = false;
                                throw;
                            }
                        }
                    }
                }
                else if (_modbusMode == "RTU")
                {
                    _serialPort = new SerialPort(_comPort)
                    {
                        BaudRate = _baudRate,
                        Parity = _parity,
                        StopBits = _stopBits,
                        DataBits = _dataBits
                    };
                    Debug.WriteLine($"[ModbusClientService] Attempting to open RTU port: {_comPort}... Instance ID: {_instanceId}");
                    _serialPort.Open();
                    _modbusMaster = Modbus.Device.ModbusSerialMaster.CreateRtu(_serialPort);
                    // ModbusMaster 생성 후 null 체크 추가 (RTU에도 동일하게 적용)
                    if (_modbusMaster == null)
                    {
                        Debug.WriteLine($"[ModbusClientService] ModbusSerialMaster.CreateRtu returned null unexpectedly. Instance ID: {_instanceId}");
                        throw new InvalidOperationException("Failed to create Modbus Master instance: CreateRtu returned null.");
                    }
                    _modbusMaster.Transport.ReadTimeout = 1000; // 읽기 타임아웃 설정
                    _modbusMaster.Transport.WriteTimeout = 1000; // 쓰기 타임아웃 설정
                    _isConnected = true;
                    Debug.WriteLine($"[ModbusClientService] RTU port opened and master created. Instance ID: {_instanceId}");
                    return true;
                }
                return false; // 이 부분은 도달하지 않아야 함
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusClientService] Connection failed: {ex.Message}. Instance ID: {_instanceId}");
                _isConnected = false;
                Dispose(); // 연결 실패 시 자원 정리
                throw; // 예외를 다시 던져 호출자에게 알림
            }
        }

        /// <summary>
        /// Modbus Coil 상태를 비동기적으로 읽습니다.
        /// </summary>
        /// <param name="startAddress">읽기 시작할 Coil 주소.</param>
        /// <param name="numberOfCoils">읽을 Coil 개수.</param>
        /// <returns>Coil 상태 배열.</returns>
        public async Task<bool[]> ReadCoilStatesAsync(ushort startAddress, ushort numberOfCoils)
        {
            if (!_isConnected || _modbusMaster == null)
            {
                Debug.WriteLine($"[ModbusClientService] Not connected. Cannot read coils. Instance ID: {_instanceId}");
                await ConnectAsync(); // 연결이 끊겼다면 재연결 시도
                if (!_isConnected || _modbusMaster == null)
                {
                    throw new InvalidOperationException("Modbus 연결이 활성화되지 않아 Coil을 읽을 수 없습니다.");
                }
            }
            try
            {
                return await _modbusMaster.ReadCoilsAsync(_slaveId, startAddress, numberOfCoils);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusClientService] Error reading coils from address {startAddress}: {ex.Message}. Instance ID: {_instanceId}");
                _isConnected = false; // 통신 오류 발생 시 연결 끊김으로 간주
                throw;
            }
        }

        /// <summary>
        /// 단일 Modbus Coil에 비동기적으로 씁니다.
        /// </summary>
        /// <param name="address">쓸 Coil 주소.</param>
        /// <param name="value">설정할 값 (true: ON, false: OFF).</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteSingleCoilAsync(ushort address, bool value)
        {
            if (!_isConnected || _modbusMaster == null)
            {
                Debug.WriteLine($"[ModbusClientService] Not connected. Cannot write single coil. Instance ID: {_instanceId}");
                await ConnectAsync(); // 연결이 끊겼다면 재연결 시도
                if (!_isConnected || _modbusMaster == null)
                {
                    throw new InvalidOperationException("Modbus 연결이 활성화되지 않아 Coil을 쓸 수 없습니다.");
                }
            }
            try
            {
                await _modbusMaster.WriteSingleCoilAsync(_slaveId, address, value);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusClientService] Error writing single coil to address {address}: {ex.Message}. Instance ID: {_instanceId}");
                _isConnected = false; // 통신 오류 발생 시 연결 끊김으로 간주
                throw;
            }
        }

        /// <summary>
        /// Modbus Discrete Input 상태를 비동기적으로 읽습니다.
        /// </summary>
        /// <param name="startAddress">읽기 시작할 Discrete Input 주소.</param>
        /// <param name="numberOfInputs">읽을 Discrete Input 개수.</param>
        /// <returns>Discrete Input 상태 배열.</returns>
        public async Task<bool[]> ReadDiscreteInputStatesAsync(ushort startAddress, ushort numberOfInputs)
        {
            if (!_isConnected || _modbusMaster == null)
            {
                Debug.WriteLine($"[ModbusClientService] Not connected. Cannot read discrete inputs. Instance ID: {_instanceId}");
                await ConnectAsync(); // 연결이 끊겼다면 재연결 시도
                if (!_isConnected || _modbusMaster == null)
                {
                    throw new InvalidOperationException("Modbus 연결이 활성화되지 않아 Discrete Input을 읽을 수 없습니다.");
                }
            }
            try
            {
                return await _modbusMaster.ReadInputsAsync(_slaveId, startAddress, numberOfInputs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusClientService] Error reading discrete inputs from address {startAddress}: {ex.Message}. Instance ID: {_instanceId}");
                _isConnected = false; // 통신 오류 발생 시 연결 끊김으로 간주
                throw;
            }
        }

        /// <summary>
        /// Modbus Holding Register 값을 비동기적으로 읽습니다.
        /// </summary>
        /// <param name="startAddress">읽기 시작할 Holding Register 주소.</param>
        /// <param name="numberOfRegisters">읽을 Holding Register 개수.</param>
        /// <returns>Holding Register 값 배열.</returns>
        public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort numberOfRegisters)
        {
            if (!_isConnected || _modbusMaster == null)
            {
                Debug.WriteLine($"[ModbusClientService] Not connected. Cannot read holding registers. Instance ID: {_instanceId}");
                await ConnectAsync(); // 연결이 끊겼다면 재연결 시도
                if (!_isConnected || _modbusMaster == null)
                {
                    throw new InvalidOperationException("Modbus 연결이 활성화되지 않아 Holding Register를 읽을 수 없습니다.");
                }
            }
            try
            {
                return await _modbusMaster.ReadHoldingRegistersAsync(_slaveId, startAddress, numberOfRegisters);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusClientService] Error reading holding registers from address {startAddress}: {ex.Message}. Instance ID: {_instanceId}");
                _isConnected = false; // 통신 오류 발생 시 연결 끊김으로 간주
                throw;
            }
        }

        /// <summary>
        /// 단일 Modbus Holding Register에 비동기적으로 씁니다.
        /// </summary>
        /// <param name="address">쓸 Holding Register 주소.</param>
        /// <param name="value">설정할 값.</param>
        /// <returns>쓰기 성공 여부.</returns>
        public async Task<bool> WriteSingleHoldingRegisterAsync(ushort address, ushort value)
        {
            if (!_isConnected || _modbusMaster == null)
            {
                Debug.WriteLine($"[ModbusClientService] Not connected. Cannot write single holding register. Instance ID: {_instanceId}");
                await ConnectAsync(); // 연결이 끊겼다면 재연결 시도
                if (!_isConnected || _modbusMaster == null)
                {
                    throw new InvalidOperationException("Modbus 연결이 활성화되지 않아 Holding Register를 쓸 수 없습니다.");
                }
            }
            try
            {
                await _modbusMaster.WriteSingleRegisterAsync(_slaveId, address, value);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusClientService] Error writing single holding register to address {address}: {ex.Message}. Instance ID: {_instanceId}");
                _isConnected = false; // 통신 오류 발생 시 연결 끊김으로 간주
                throw;
            }
        }

        /// <summary>
        /// Modbus 마스터 및 클라이언트/시리얼 포트 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            if (_modbusMaster != null)
            {
                _modbusMaster.Dispose();
                _modbusMaster = null;
                Debug.WriteLine($"[ModbusClientService] Modbus Master disposed. Instance ID: {_instanceId}");
            }
            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient.Dispose();
                _tcpClient = null;
                Debug.WriteLine($"[ModbusClientService] TCP Client disposed. Instance ID: {_instanceId}");
            }
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
                Debug.WriteLine($"[ModbusClientService] Serial Port disposed. Instance ID: {_instanceId}");
            }
            _isConnected = false;
            Debug.WriteLine($"[ModbusClientService] Disposed. Instance ID: {_instanceId}");
        }
    }
}
