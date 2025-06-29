// Services/HttpService.cs
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WPF_WMS01.Models; // 이 네임스페이스에 LoginRequest, LoginResponse, ApiVersion이 있다고 가정합니다.

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
            _httpClient = new HttpClient();
            BaseApiUrl = baseApiUrl;
            _httpClient.BaseAddress = new Uri(baseApiUrl);
        }

        /// <summary>
        /// 서버 응답에 따라 후속 요청에 대한 API 버전을 설정합니다.
        /// </summary>
        /// <param name="major">주 버전 번호.</param>
        /// <param name="minor">부 버전 번호.</param>
        public void SetCurrentApiVersion(int major, int minor)
        {
            _currentApiVersionMajor = major;
            _currentApiVersionMinor = minor;
            // 디버그 메시지를 수정하여 실제 설정된 버전을 정확히 출력합니다.
            Debug.WriteLine($"HttpService API 버전이 v{_currentApiVersionMajor}.{_currentApiVersionMinor}로 설정되었습니다.");
        }

        public void SetAuthorizationHeader(string token)
        {
            _authToken = token;
            // HttpService는 싱글톤이므로, 토큰을 받으면 모든 요청에 이 헤더를 사용합니다.
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
        }

        public void ClearAuthorizationHeader()
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _authToken = null;
        }

        /// <summary>
        /// 동적 API 버전 관리를 사용하여 지정된 엔드포인트로 POST 요청을 보냅니다 (로그인 호출 제외).
        /// </summary>
        /// <typeparam name="TRequest">요청 데이터의 타입.</typeparam>
        /// <typeparam name="TResponse">예상 응답의 타입.</typeparam>
        /// <param name="endpoint">API 엔드포인트 (예: "missions", "wms/rest/login").</param>
        /// <param name="data">요청 데이터 객체.</param>
        /// <returns>역직렬화된 응답 객체.</returns>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            string fullEndpoint = endpoint;
            // 특정 로그인 엔드포인트가 아닌 경우에만 버전 관리를 적용합니다.
            // 제공된 로그인 URL은 "wms/rest/login"이므로 정확히 일치하거나 이로 시작하는지 확인합니다.
            if (!endpoint.Equals("wms/rest/login", StringComparison.OrdinalIgnoreCase))
            {
                // "missions"와 같은 다른 엔드포인트의 경우 버전이 포함된 경로를 구성합니다: wms/rest/vX.Y/{endpoint}
                fullEndpoint = $"wms/rest/v{_currentApiVersionMajor}.{_currentApiVersionMinor}/{endpoint}";
            }
            // else: fullEndpoint는 "wms/rest/login"으로 유지됩니다.

            var jsonContent = JsonConvert.SerializeObject(data, _jsonSettings);

            Debug.WriteLine($"POST 요청 전송 대상: {BaseApiUrl}{fullEndpoint}");
            Debug.WriteLine($"요청 본문 (JSON): {jsonContent}");

            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(fullEndpoint, httpContent);

            // 오류 발생 시 전체 오류 응답을 로깅하여 디버깅을 돕습니다.
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API 오류 응답 ({response.StatusCode}): {errorContent}");
                response.EnsureSuccessStatusCode(); // 여전히 비-성공 상태 코드에 대해 예외를 발생시킵니다.
            }

            var responseString = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"응답 본문: {responseString}");

            return JsonConvert.DeserializeObject<TResponse>(responseString, _jsonSettings);
        }

        /// <summary>
        /// 동적 API 버전 관리를 사용하여 지정된 엔드포인트로 GET 요청을 보냅니다.
        /// </summary>
        /// <typeparam name="T">예상 응답의 타입.</typeparam>
        /// <param name="endpoint">API 엔드포인트 (예: "missions/{mission_id}").</param>
        /// <returns>역직렬화된 응답 객체.</returns>
        public async Task<T> GetAsync<T>(string endpoint)
        {
            // GET 요청의 경우, 모든 비-로그인 경로에 버전 관리가 적용된다고 가정합니다.
            // 예: endpoint가 "missions/{mission_id}"인 경우 "wms/rest/vX.Y/missions/{mission_id}"가 됩니다.
            string fullEndpoint = $"wms/rest/v{_currentApiVersionMajor}.{_currentApiVersionMinor}/{endpoint}";

            HttpResponseMessage response = await _httpClient.GetAsync(fullEndpoint);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API 오류 응답 ({response.StatusCode}): {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GET 응답 본문 ({fullEndpoint}): {responseBody}");

            return JsonConvert.DeserializeObject<T>(responseBody, _jsonSettings);
        }


        // POST 요청 (응답 본문이 없을 경우 또는 응답 타입을 특정하지 않는 경우)
        public async Task PostAsync<TRequest>(string endpoint, TRequest data)
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(data);
                StringContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();
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

        // 새 명세에 맞는 Login 메서드 (옵션: MainViewModel에서 직접 PostAsync 호출해도 됨)
        public async Task<LoginResponse> Login(string username, string password)
        {
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password,
                ApiVersion = new ApiVersion { Major = 0, Minor = 0 } // API 버전 정보 추가
            };
            // 변경된 엔드포인트 사용
            return await PostAsync<LoginRequest, LoginResponse>("wms/rest/login", loginRequest);
        }
    }
}
