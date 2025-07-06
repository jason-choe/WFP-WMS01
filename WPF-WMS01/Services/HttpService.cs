// Services/HttpService.cs
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WPF_WMS01.Models; // RobotMissionModels.cs에 정의된 모델들 포함
using System.Threading; // CancellationToken을 위해 추가

namespace WPF_WMS01.Services
{
    public class HttpService
    {
        private readonly HttpClient _httpClient;
        private string _authToken;
        private int _currentApiVersionMajor = 0;
        private int _currentApiVersionMinor = 0;

        // MainViewModel에서 현재 API 버전에 접근할 수 있는 공개 속성
        public int CurrentApiVersionMajor => _currentApiVersionMajor;
        public int CurrentApiVersionMinor => _currentApiVersionMinor;
        public string BaseApiUrl { get; }

        // Newtonsoft.Json 직렬화/역직렬화를 위한 설정 객체를 static으로 선언하여 재사용
        private static readonly JsonSerializerSettings _jsonSettings;
        static HttpService() // static 생성자에서 설정 객체 초기화
        {
            _jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(), // <--- PascalCase를 camelCase로 변환
                // Null 값을 직렬화에 포함할지 여부 등 다른 설정도 필요하면 여기에 추가
                NullValueHandling = NullValueHandling.Ignore // 예시: null 값은 JSON에 포함하지 않음
            };
        }

        public HttpService(string baseApiUrl)
        {
            BaseApiUrl = baseApiUrl ?? throw new ArgumentNullException(nameof(baseApiUrl));
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseApiUrl) };
            // 모든 요청에 대해 JSON을 camelCase로 직렬화하도록 설정
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // 인증 토큰 설정 메서드
        public void SetAuthorizationHeader(string token)
        {
            _authToken = token;
            if (!string.IsNullOrEmpty(_authToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                Debug.WriteLine("Authorization header set.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                Debug.WriteLine("Authorization header cleared.");
            }
        }

        // 현재 API 버전 설정 메서드
        public void SetCurrentApiVersion(int major, int minor)
        {
            _currentApiVersionMajor = major;
            _currentApiVersionMinor = minor;
            Debug.WriteLine($"API Version set to v{_currentApiVersionMajor}.{_currentApiVersionMinor}");
        }

        // HTTP GET 요청을 보내고 응답을 역직렬화
        public async Task<TResponse> GetAsync<TResponse>(string endpoint)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode(); // 200-299 외의 상태 코드는 예외 발생

                string responseBody = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TResponse>(responseBody, _jsonSettings);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"GET 요청 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 역직렬화 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"예상치 못한 오류 ({endpoint}): {ex.Message}");
                throw;
            }
        }

        // HTTP POST 요청을 보내고 응답을 역직렬화 (CancellationToken 없음)
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            // CancellationToken.None을 사용하여 CancellationToken을 받는 오버로드를 호출
            return await PostAsync<TRequest, TResponse>(endpoint, data, CancellationToken.None);
        }

        // HTTP POST 요청을 보내고 응답을 역직렬화 (CancellationToken 포함)
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken)
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(data, _jsonSettings); // static _jsonSettings 사용
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content, cancellationToken); // CancellationToken 전달
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TResponse>(responseBody, _jsonSettings); // static _jsonSettings 사용
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine($"POST request to {endpoint} was cancelled: {ex.Message}");
                throw; // 취소 예외는 그대로 다시 던짐
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"POST 요청 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 직렬화/역직렬화 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"예상치 못한 오류 ({endpoint}): {ex.Message}");
                throw;
            }
        }

        // HTTP POST 요청 (응답 본문이 없는 경우)
        public async Task PostAsync<TRequest>(string endpoint, TRequest data)
        {
            // CancellationToken.None을 사용하여 CancellationToken을 받는 오버로드를 호출
            await PostAsync(endpoint, data, CancellationToken.None);
        }

        // HTTP POST 요청 (응답 본문이 없는 경우, CancellationToken 포함)
        public async Task PostAsync<TRequest>(string endpoint, TRequest data, CancellationToken cancellationToken)
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(data, _jsonSettings); // static _jsonSettings 사용
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine($"POST request to {endpoint} was cancelled: {ex.Message}");
                throw; // 취소 예외는 그대로 다시 던짐
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"POST 요청 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 직렬화 오류 ({endpoint}): {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"예상치 못한 오류 ({endpoint}): {ex.Message}");
                throw;
            }
        }

        // 필요하다면 PUT, DELETE 등의 메서드도 유사하게 추가할 수 있습니다.
        // 새 명세에 맞는 Login 메서드 (옵션: MainViewModel에서 직접 PostAsync 호출해도 됨)
        /*public async Task<LoginResponse> Login(string username, string password)
        {
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password,
                ApiVersion = new ApiVersion { Major = 0, Minor = 0 } // API 버전 정보 추가
            };
            // 변경된 엔드포인트 사용
            return await PostAsync<LoginRequest, LoginResponse>("wms/rest/login", loginRequest);
        }*/
    }
}
