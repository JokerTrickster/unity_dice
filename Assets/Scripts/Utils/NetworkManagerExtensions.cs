using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// NetworkManager의 확장 메서드
/// 기존 콜백 기반 API를 async/await 패턴으로 래핑하여 사용 편의성을 높입니다.
/// </summary>
public static class NetworkManagerExtensions
{
    #region Async Wrapper Methods
    /// <summary>
    /// GET 요청 (비동기)
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>API 응답</returns>
    public static Task<ApiResponse<T>> GetAsync<T>(this NetworkManager networkManager, string endpoint, float timeout = 0f) 
        where T : class
    {
        var tcs = new TaskCompletionSource<ApiResponse<T>>();

        try
        {
            networkManager.Get(endpoint, (response) =>
            {
                var apiResponse = CreateApiResponse<T>(response);
                tcs.SetResult(apiResponse);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// POST 요청 (비동기)
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>API 응답</returns>
    public static Task<ApiResponse<T>> PostAsync<T>(this NetworkManager networkManager, string endpoint, object data, float timeout = 0f) 
        where T : class
    {
        var tcs = new TaskCompletionSource<ApiResponse<T>>();

        try
        {
            networkManager.Post(endpoint, data, (response) =>
            {
                var apiResponse = CreateApiResponse<T>(response);
                tcs.SetResult(apiResponse);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// PUT 요청 (비동기)
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>API 응답</returns>
    public static Task<ApiResponse<T>> PutAsync<T>(this NetworkManager networkManager, string endpoint, object data, float timeout = 0f) 
        where T : class
    {
        var tcs = new TaskCompletionSource<ApiResponse<T>>();

        try
        {
            networkManager.Put(endpoint, data, (response) =>
            {
                var apiResponse = CreateApiResponse<T>(response);
                tcs.SetResult(apiResponse);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// DELETE 요청 (비동기)
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>API 응답</returns>
    public static Task<ApiResponse<T>> DeleteAsync<T>(this NetworkManager networkManager, string endpoint, float timeout = 0f) 
        where T : class
    {
        var tcs = new TaskCompletionSource<ApiResponse<T>>();

        try
        {
            networkManager.Delete(endpoint, (response) =>
            {
                var apiResponse = CreateApiResponse<T>(response);
                tcs.SetResult(apiResponse);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// PATCH 요청 (비동기)
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>API 응답</returns>
    public static Task<ApiResponse<T>> PatchAsync<T>(this NetworkManager networkManager, string endpoint, object data, float timeout = 0f) 
        where T : class
    {
        var tcs = new TaskCompletionSource<ApiResponse<T>>();

        try
        {
            networkManager.Patch(endpoint, data, (response) =>
            {
                var apiResponse = CreateApiResponse<T>(response);
                tcs.SetResult(apiResponse);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }
    #endregion

    #region Non-Generic Async Methods
    /// <summary>
    /// GET 요청 (비동기, 제네릭 없음)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>네트워크 응답</returns>
    public static Task<NetworkResponse> GetAsync(this NetworkManager networkManager, string endpoint, float timeout = 0f)
    {
        var tcs = new TaskCompletionSource<NetworkResponse>();

        try
        {
            networkManager.Get(endpoint, (response) =>
            {
                tcs.SetResult(response);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// POST 요청 (비동기, 제네릭 없음)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>네트워크 응답</returns>
    public static Task<NetworkResponse> PostAsync(this NetworkManager networkManager, string endpoint, object data, float timeout = 0f)
    {
        var tcs = new TaskCompletionSource<NetworkResponse>();

        try
        {
            networkManager.Post(endpoint, data, (response) =>
            {
                tcs.SetResult(response);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// PUT 요청 (비동기, 제네릭 없음)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>네트워크 응답</returns>
    public static Task<NetworkResponse> PutAsync(this NetworkManager networkManager, string endpoint, object data, float timeout = 0f)
    {
        var tcs = new TaskCompletionSource<NetworkResponse>();

        try
        {
            networkManager.Put(endpoint, data, (response) =>
            {
                tcs.SetResult(response);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// DELETE 요청 (비동기, 제네릭 없음)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>네트워크 응답</returns>
    public static Task<NetworkResponse> DeleteAsync(this NetworkManager networkManager, string endpoint, float timeout = 0f)
    {
        var tcs = new TaskCompletionSource<NetworkResponse>();

        try
        {
            networkManager.Delete(endpoint, (response) =>
            {
                tcs.SetResult(response);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// PATCH 요청 (비동기, 제네릭 없음)
    /// </summary>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeout">타임아웃 (초)</param>
    /// <returns>네트워크 응답</returns>
    public static Task<NetworkResponse> PatchAsync(this NetworkManager networkManager, string endpoint, object data, float timeout = 0f)
    {
        var tcs = new TaskCompletionSource<NetworkResponse>();

        try
        {
            networkManager.Patch(endpoint, data, (response) =>
            {
                tcs.SetResult(response);
            }, timeout);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }
    #endregion

    #region Timeout Overloads
    /// <summary>
    /// 타임아웃 지정이 있는 POST 요청 (비동기)
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkManager">NetworkManager 인스턴스</param>
    /// <param name="endpoint">요청 엔드포인트</param>
    /// <param name="data">요청 데이터</param>
    /// <param name="timeoutSeconds">타임아웃 (초)</param>
    /// <returns>API 응답</returns>
    public static Task<ApiResponse<T>> PostAsync<T>(this NetworkManager networkManager, string endpoint, object data, float timeoutSeconds) 
        where T : class
    {
        return PostAsync<T>(networkManager, endpoint, data, timeoutSeconds);
    }
    #endregion

    #region Private Helper Methods
    /// <summary>
    /// NetworkResponse를 ApiResponse로 변환
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    /// <param name="networkResponse">네트워크 응답</param>
    /// <returns>API 응답</returns>
    private static ApiResponse<T> CreateApiResponse<T>(NetworkResponse networkResponse) where T : class
    {
        var apiResponse = new ApiResponse<T>
        {
            IsSuccess = networkResponse.IsSuccess,
            statusCode = networkResponse.StatusCode,
            errorMessage = networkResponse.Error
        };

        if (networkResponse.IsSuccess && !string.IsNullOrEmpty(networkResponse.RawData))
        {
            try
            {
                apiResponse.data = JsonUtility.FromJson<T>(networkResponse.RawData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManagerExtensions] Failed to parse response data: {ex.Message}");
                apiResponse.IsSuccess = false;
                apiResponse.errorMessage = $"Failed to parse response: {ex.Message}";
            }
        }

        return apiResponse;
    }
    #endregion
}

#region Data Classes
/// <summary>
/// API 응답 래퍼 클래스
/// 기존 Authentication 폴더 컴포넌트와의 호환성을 위해 제공됩니다.
/// </summary>
/// <typeparam name="T">응답 데이터 타입</typeparam>
[Serializable]
public class ApiResponse<T> where T : class
{
    public bool IsSuccess;
    public T data;
    public long statusCode;
    public string errorMessage;
}
#endregion