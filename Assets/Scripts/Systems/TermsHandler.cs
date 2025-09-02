using System;
using UnityEngine;

/// <summary>
/// TermsHandler - 이용약관 및 개인정보 처리방침 표시 시스템
/// 플랫폼별로 적절한 방식으로 약관을 표시합니다.
/// 모바일: 네이티브 웹뷰, 데스크톱: 스크롤뷰 또는 브라우저
/// </summary>
public class TermsHandler : MonoBehaviour
{
    #region Constants
    private const string TERMS_URL = "https://unitydice.com/terms";
    private const string PRIVACY_URL = "https://unitydice.com/privacy";
    private const string TERMS_RESOURCE_PATH = "Legal/terms_kr";
    private const string PRIVACY_RESOURCE_PATH = "Legal/privacy_kr";
    #endregion

    #region Events
    /// <summary>
    /// 약관 표시 시작 시 발생하는 이벤트
    /// </summary>
    public static event Action<TermsType> OnTermsDisplayStarted;
    
    /// <summary>
    /// 약관 표시 완료 시 발생하는 이벤트
    /// </summary>
    public static event Action<TermsType, bool> OnTermsDisplayCompleted;
    #endregion

    #region Private Fields
    private bool _isDisplayingTerms = false;
    private TermsDisplayMethod _currentDisplayMethod = TermsDisplayMethod.None;
    #endregion

    #region Properties
    /// <summary>
    /// 현재 약관 표시 중 여부
    /// </summary>
    public bool IsDisplayingTerms => _isDisplayingTerms;

    /// <summary>
    /// 현재 사용 중인 표시 방법
    /// </summary>
    public TermsDisplayMethod CurrentDisplayMethod => _currentDisplayMethod;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        // 이벤트 정리
        OnTermsDisplayStarted = null;
        OnTermsDisplayCompleted = null;
    }
    #endregion

    #region Public API
    /// <summary>
    /// 이용약관 표시
    /// </summary>
    public void ShowTermsAndConditions()
    {
        ShowTerms(TermsType.TermsAndConditions, TERMS_URL, TERMS_RESOURCE_PATH);
    }

    /// <summary>
    /// 개인정보 처리방침 표시
    /// </summary>
    public void ShowPrivacyPolicy()
    {
        ShowTerms(TermsType.PrivacyPolicy, PRIVACY_URL, PRIVACY_RESOURCE_PATH);
    }

    /// <summary>
    /// 약관 표시 닫기
    /// </summary>
    public void CloseTermsDisplay()
    {
        if (!_isDisplayingTerms)
        {
            Debug.LogWarning("[TermsHandler] No terms currently being displayed");
            return;
        }

        try
        {
            CloseCurrentTermsDisplay();
            Debug.Log("[TermsHandler] Terms display closed by user");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TermsHandler] Failed to close terms display: {ex.Message}");
        }
    }
    #endregion

    #region Terms Display
    /// <summary>
    /// 약관 표시 (플랫폼별 적절한 방식 선택)
    /// </summary>
    /// <param name="termsType">약관 유형</param>
    /// <param name="url">온라인 URL</param>
    /// <param name="resourcePath">로컬 리소스 경로</param>
    private void ShowTerms(TermsType termsType, string url, string resourcePath)
    {
        if (_isDisplayingTerms)
        {
            Debug.LogWarning("[TermsHandler] Terms already being displayed");
            return;
        }

        try
        {
            Debug.Log($"[TermsHandler] Showing {termsType}...");
            _isDisplayingTerms = true;
            OnTermsDisplayStarted?.Invoke(termsType);

            // 플랫폼에 따른 표시 방법 결정
            _currentDisplayMethod = DetermineDisplayMethod();

            switch (_currentDisplayMethod)
            {
                case TermsDisplayMethod.NativeWebView:
                    ShowTermsInNativeWebView(url, termsType);
                    break;
                    
                case TermsDisplayMethod.ExternalBrowser:
                    ShowTermsInExternalBrowser(url, termsType);
                    break;
                    
                case TermsDisplayMethod.LocalScrollView:
                    ShowTermsInLocalScrollView(resourcePath, termsType);
                    break;
                    
                case TermsDisplayMethod.UIModal:
                    ShowTermsInUIModal(resourcePath, termsType);
                    break;
                    
                default:
                    Debug.LogError($"[TermsHandler] Unsupported display method: {_currentDisplayMethod}");
                    OnTermsDisplayError(termsType, "지원하지 않는 표시 방법입니다.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TermsHandler] Failed to show terms: {ex.Message}");
            OnTermsDisplayError(termsType, ex.Message);
        }
    }

    /// <summary>
    /// 플랫폼에 따른 표시 방법 결정
    /// </summary>
    private TermsDisplayMethod DetermineDisplayMethod()
    {
        RuntimePlatform platform = Application.platform;
        
        switch (platform)
        {
            case RuntimePlatform.Android:
            case RuntimePlatform.IPhonePlayer:
                // 모바일: 네이티브 웹뷰 우선, 실패 시 외부 브라우저
                return IsNativeWebViewAvailable() ? TermsDisplayMethod.NativeWebView : TermsDisplayMethod.ExternalBrowser;
                
            case RuntimePlatform.WebGLPlayer:
                // WebGL: 외부 브라우저만 가능
                return TermsDisplayMethod.ExternalBrowser;
                
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.LinuxPlayer:
                // 데스크톱: 로컬 스크롤뷰 우선, 실패 시 외부 브라우저
                return AreLocalResourcesAvailable() ? TermsDisplayMethod.LocalScrollView : TermsDisplayMethod.ExternalBrowser;
                
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.LinuxEditor:
                // 에디터: UI 모달로 테스트
                return TermsDisplayMethod.UIModal;
                
            default:
                Debug.LogWarning($"[TermsHandler] Unknown platform: {platform}, using external browser");
                return TermsDisplayMethod.ExternalBrowser;
        }
    }

    /// <summary>
    /// 네이티브 웹뷰 사용 가능 여부 확인
    /// </summary>
    private bool IsNativeWebViewAvailable()
    {
        // 실제 구현에서는 네이티브 웹뷰 플러그인 확인
        // 현재는 모바일 플랫폼에서 기본적으로 사용 가능하다고 가정
        #if UNITY_ANDROID || UNITY_IOS
            return true;
        #else
            return false;
        #endif
    }

    /// <summary>
    /// 로컬 리소스 사용 가능 여부 확인
    /// </summary>
    private bool AreLocalResourcesAvailable()
    {
        try
        {
            var termsAsset = Resources.Load<TextAsset>(TERMS_RESOURCE_PATH.Replace("Legal/", ""));
            var privacyAsset = Resources.Load<TextAsset>(PRIVACY_RESOURCE_PATH.Replace("Legal/", ""));
            
            return termsAsset != null && privacyAsset != null;
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region Display Methods
    /// <summary>
    /// 네이티브 웹뷰에서 약관 표시
    /// </summary>
    private void ShowTermsInNativeWebView(string url, TermsType termsType)
    {
        try
        {
            Debug.Log($"[TermsHandler] Opening {termsType} in native webview: {url}");
            
            // 네이티브 웹뷰 구현 (실제로는 플러그인 사용)
            #if UNITY_ANDROID || UNITY_IOS
                // 네이티브 웹뷰 플러그인 호출
                // UniWebView 또는 다른 웹뷰 플러그인 사용
                Application.OpenURL(url); // 임시로 외부 브라우저 사용
            #endif
            
            OnTermsDisplayCompleted?.Invoke(termsType, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TermsHandler] Native webview failed: {ex.Message}");
            OnTermsDisplayError(termsType, ex.Message);
        }
        finally
        {
            _isDisplayingTerms = false;
            _currentDisplayMethod = TermsDisplayMethod.None;
        }
    }

    /// <summary>
    /// 외부 브라우저에서 약관 표시
    /// </summary>
    private void ShowTermsInExternalBrowser(string url, TermsType termsType)
    {
        try
        {
            Debug.Log($"[TermsHandler] Opening {termsType} in external browser: {url}");
            
            // 외부 브라우저에서 열기
            Application.OpenURL(url);
            
            OnTermsDisplayCompleted?.Invoke(termsType, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TermsHandler] External browser failed: {ex.Message}");
            OnTermsDisplayError(termsType, ex.Message);
        }
        finally
        {
            _isDisplayingTerms = false;
            _currentDisplayMethod = TermsDisplayMethod.None;
        }
    }

    /// <summary>
    /// 로컬 스크롤뷰에서 약관 표시
    /// </summary>
    private void ShowTermsInLocalScrollView(string resourcePath, TermsType termsType)
    {
        try
        {
            Debug.Log($"[TermsHandler] Loading {termsType} from local resources: {resourcePath}");
            
            // 로컬 리소스에서 텍스트 로드
            string fileName = resourcePath.Replace("Legal/", "");
            TextAsset termsAsset = Resources.Load<TextAsset>(fileName);
            
            if (termsAsset == null)
            {
                throw new Exception($"Terms resource not found: {resourcePath}");
            }

            // UIManager를 통한 스크롤뷰 모달 표시
            ShowTermsInScrollView(termsAsset.text, termsType);
            
            OnTermsDisplayCompleted?.Invoke(termsType, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TermsHandler] Local scroll view failed: {ex.Message}");
            OnTermsDisplayError(termsType, ex.Message);
        }
        finally
        {
            _isDisplayingTerms = false;
            _currentDisplayMethod = TermsDisplayMethod.None;
        }
    }

    /// <summary>
    /// UI 모달에서 약관 표시
    /// </summary>
    private void ShowTermsInUIModal(string resourcePath, TermsType termsType)
    {
        try
        {
            Debug.Log($"[TermsHandler] Showing {termsType} in UI modal");
            
            // 로컬 리소스 로드 시도
            string fileName = resourcePath.Replace("Legal/", "");
            TextAsset termsAsset = Resources.Load<TextAsset>(fileName);
            
            string content = termsAsset?.text ?? GetDefaultTermsContent(termsType);
            
            // UIManager를 통한 모달 표시
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowTextModal(
                    title: GetTermsTitle(termsType),
                    content: content,
                    onClose: () => OnTermsModalClosed(termsType)
                );
            }
            else
            {
                // UIManager가 없는 경우 콘솔에 출력 (개발/테스트 용도)
                Debug.Log($"[TermsHandler] {GetTermsTitle(termsType)}:\n{content}");
                OnTermsDisplayCompleted?.Invoke(termsType, true);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TermsHandler] UI modal failed: {ex.Message}");
            OnTermsDisplayError(termsType, ex.Message);
        }
    }

    /// <summary>
    /// 스크롤뷰에서 약관 표시
    /// </summary>
    private void ShowTermsInScrollView(string content, TermsType termsType)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowScrollableTextModal(
                title: GetTermsTitle(termsType),
                content: content,
                onClose: () => OnTermsModalClosed(termsType)
            );
        }
        else
        {
            Debug.Log($"[TermsHandler] {GetTermsTitle(termsType)}:\n{content}");
            OnTermsDisplayCompleted?.Invoke(termsType, true);
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 약관 유형별 제목 반환
    /// </summary>
    private string GetTermsTitle(TermsType termsType)
    {
        return termsType switch
        {
            TermsType.TermsAndConditions => "이용약관",
            TermsType.PrivacyPolicy => "개인정보 처리방침",
            _ => "약관"
        };
    }

    /// <summary>
    /// 기본 약관 내용 반환 (리소스가 없는 경우)
    /// </summary>
    private string GetDefaultTermsContent(TermsType termsType)
    {
        return termsType switch
        {
            TermsType.TermsAndConditions => "이용약관 내용을 불러오는 중입니다...\n\n자세한 내용은 공식 웹사이트를 참조하세요.",
            TermsType.PrivacyPolicy => "개인정보 처리방침 내용을 불러오는 중입니다...\n\n자세한 내용은 공식 웹사이트를 참조하세요.",
            _ => "약관 내용을 불러오는 중입니다..."
        };
    }

    /// <summary>
    /// 약관 모달 닫기 콜백
    /// </summary>
    private void OnTermsModalClosed(TermsType termsType)
    {
        _isDisplayingTerms = false;
        _currentDisplayMethod = TermsDisplayMethod.None;
        OnTermsDisplayCompleted?.Invoke(termsType, true);
        
        Debug.Log($"[TermsHandler] {termsType} modal closed by user");
    }

    /// <summary>
    /// 현재 약관 표시 닫기
    /// </summary>
    private void CloseCurrentTermsDisplay()
    {
        switch (_currentDisplayMethod)
        {
            case TermsDisplayMethod.UIModal:
            case TermsDisplayMethod.LocalScrollView:
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.CloseCurrentModal();
                }
                break;
                
            case TermsDisplayMethod.NativeWebView:
                // 네이티브 웹뷰 닫기
                break;
                
            case TermsDisplayMethod.ExternalBrowser:
                // 외부 브라우저는 사용자가 직접 닫아야 함
                break;
        }
        
        _isDisplayingTerms = false;
        _currentDisplayMethod = TermsDisplayMethod.None;
    }

    /// <summary>
    /// 약관 표시 에러 처리
    /// </summary>
    private void OnTermsDisplayError(TermsType termsType, string errorMessage)
    {
        _isDisplayingTerms = false;
        _currentDisplayMethod = TermsDisplayMethod.None;
        OnTermsDisplayCompleted?.Invoke(termsType, false);
        
        // 사용자에게 에러 알림
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowErrorDialog(
                title: "약관 표시 실패",
                message: $"약관을 표시하는 중 문제가 발생했습니다.\n{errorMessage}",
                onRetry: null,
                onCancel: null
            );
        }
    }

    /// <summary>
    /// 약관 핸들러 상태 반환
    /// </summary>
    public TermsHandlerStatus GetStatus()
    {
        return new TermsHandlerStatus
        {
            IsDisplayingTerms = _isDisplayingTerms,
            CurrentDisplayMethod = _currentDisplayMethod,
            IsNativeWebViewSupported = IsNativeWebViewAvailable(),
            AreLocalResourcesAvailable = AreLocalResourcesAvailable(),
            Platform = Application.platform
        };
    }
    #endregion
}

#region Data Classes
/// <summary>
/// 약관 유형
/// </summary>
public enum TermsType
{
    TermsAndConditions,
    PrivacyPolicy
}

/// <summary>
/// 약관 표시 방법
/// </summary>
public enum TermsDisplayMethod
{
    None,
    NativeWebView,      // 모바일 네이티브 웹뷰
    ExternalBrowser,    // 외부 브라우저
    LocalScrollView,    // 로컬 리소스 스크롤뷰
    UIModal             // UI 모달
}

/// <summary>
/// TermsHandler 상태 정보
/// </summary>
[Serializable]
public class TermsHandlerStatus
{
    public bool IsDisplayingTerms { get; set; }
    public TermsDisplayMethod CurrentDisplayMethod { get; set; }
    public bool IsNativeWebViewSupported { get; set; }
    public bool AreLocalResourcesAvailable { get; set; }
    public RuntimePlatform Platform { get; set; }
}
#endregion