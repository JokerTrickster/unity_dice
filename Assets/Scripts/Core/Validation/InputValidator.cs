using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 입력 유효성 검증 시스템
/// 다양한 사용자 입력에 대한 검증 규칙과 로직을 제공합니다.
/// </summary>
public static class InputValidator
{
    #region Constants
    private const int MIN_NICKNAME_LENGTH = 2;
    private const int MAX_NICKNAME_LENGTH = 12;
    private const int MIN_PASSWORD_LENGTH = 8;
    private const int MAX_PASSWORD_LENGTH = 20;
    
    // 정규표현식 패턴
    private static readonly Regex EmailPattern = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
    private static readonly Regex NicknamePattern = new(@"^[가-힣a-zA-Z0-9_-]+$");
    private static readonly Regex PasswordPattern = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]+$");
    private static readonly Regex PhonePattern = new(@"^01[016789]-?\d{3,4}-?\d{4}$");
    
    // 금지된 단어 목록 (실제로는 더 많은 단어가 포함되어야 함)
    private static readonly HashSet<string> ProhibitedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "root", "system", "test", "guest",
        "fuck", "shit", "damn", "hell", "ass", "bitch", "bastard",
        "관리자", "테스트", "시스템", "게스트", "운영자",
        "씨발", "개새끼", "병신", "바보", "멍청이"
    };
    #endregion

    #region Validation Results
    /// <summary>
    /// 검증 결과 클래스
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public ValidationErrorType ErrorType { get; set; }
        public string Field { get; set; }
        
        public static ValidationResult Success() => new() { IsValid = true };
        
        public static ValidationResult Error(string message, ValidationErrorType errorType, string field = "")
        {
            return new()
            {
                IsValid = false,
                ErrorMessage = message,
                ErrorType = errorType,
                Field = field
            };
        }
    }

    /// <summary>
    /// 검증 오류 타입
    /// </summary>
    public enum ValidationErrorType
    {
        Required,
        Length,
        Format,
        Pattern,
        Prohibited,
        Range,
        Duplicate,
        System
    }
    #endregion

    #region Nickname Validation
    /// <summary>
    /// 닉네임 유효성 검증
    /// </summary>
    public static ValidationResult ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return ValidationResult.Error("닉네임을 입력해주세요.", ValidationErrorType.Required, "nickname");
        }

        // 길이 검증
        if (nickname.Length < MIN_NICKNAME_LENGTH)
        {
            return ValidationResult.Error($"닉네임은 최소 {MIN_NICKNAME_LENGTH}자 이상이어야 합니다.", 
                                        ValidationErrorType.Length, "nickname");
        }

        if (nickname.Length > MAX_NICKNAME_LENGTH)
        {
            return ValidationResult.Error($"닉네임은 최대 {MAX_NICKNAME_LENGTH}자까지 입력할 수 있습니다.", 
                                        ValidationErrorType.Length, "nickname");
        }

        // 패턴 검증 (한글, 영문, 숫자, _, - 만 허용)
        if (!NicknamePattern.IsMatch(nickname))
        {
            return ValidationResult.Error("닉네임은 한글, 영문, 숫자, '_', '-'만 사용할 수 있습니다.", 
                                        ValidationErrorType.Pattern, "nickname");
        }

        // 금지된 단어 검증
        if (ContainsProhibitedWords(nickname))
        {
            return ValidationResult.Error("사용할 수 없는 닉네임입니다.", 
                                        ValidationErrorType.Prohibited, "nickname");
        }

        // 연속된 특수문자 검증
        if (HasConsecutiveSpecialChars(nickname))
        {
            return ValidationResult.Error("특수문자는 연속으로 사용할 수 없습니다.", 
                                        ValidationErrorType.Pattern, "nickname");
        }

        // 시작/끝 특수문자 검증
        if (nickname.StartsWith("_") || nickname.StartsWith("-") || 
            nickname.EndsWith("_") || nickname.EndsWith("-"))
        {
            return ValidationResult.Error("닉네임은 특수문자로 시작하거나 끝날 수 없습니다.", 
                                        ValidationErrorType.Pattern, "nickname");
        }

        return ValidationResult.Success();
    }
    #endregion

    #region Email Validation
    /// <summary>
    /// 이메일 유효성 검증
    /// </summary>
    public static ValidationResult ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return ValidationResult.Error("이메일 주소를 입력해주세요.", ValidationErrorType.Required, "email");
        }

        if (!EmailPattern.IsMatch(email))
        {
            return ValidationResult.Error("올바른 이메일 형식이 아닙니다.", ValidationErrorType.Format, "email");
        }

        if (email.Length > 100)
        {
            return ValidationResult.Error("이메일 주소가 너무 깁니다.", ValidationErrorType.Length, "email");
        }

        return ValidationResult.Success();
    }
    #endregion

    #region Password Validation
    /// <summary>
    /// 비밀번호 유효성 검증
    /// </summary>
    public static ValidationResult ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return ValidationResult.Error("비밀번호를 입력해주세요.", ValidationErrorType.Required, "password");
        }

        if (password.Length < MIN_PASSWORD_LENGTH)
        {
            return ValidationResult.Error($"비밀번호는 최소 {MIN_PASSWORD_LENGTH}자 이상이어야 합니다.", 
                                        ValidationErrorType.Length, "password");
        }

        if (password.Length > MAX_PASSWORD_LENGTH)
        {
            return ValidationResult.Error($"비밀번호는 최대 {MAX_PASSWORD_LENGTH}자까지 입력할 수 있습니다.", 
                                        ValidationErrorType.Length, "password");
        }

        if (!PasswordPattern.IsMatch(password))
        {
            return ValidationResult.Error("비밀번호는 대문자, 소문자, 숫자, 특수문자를 포함해야 합니다.", 
                                        ValidationErrorType.Pattern, "password");
        }

        // 연속된 문자 검증
        if (HasConsecutiveChars(password, 3))
        {
            return ValidationResult.Error("동일한 문자를 3회 이상 연속으로 사용할 수 없습니다.", 
                                        ValidationErrorType.Pattern, "password");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 비밀번호 확인 검증
    /// </summary>
    public static ValidationResult ValidatePasswordConfirm(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            return ValidationResult.Error("비밀번호 확인을 입력해주세요.", ValidationErrorType.Required, "confirmPassword");
        }

        if (password != confirmPassword)
        {
            return ValidationResult.Error("비밀번호가 일치하지 않습니다.", ValidationErrorType.Pattern, "confirmPassword");
        }

        return ValidationResult.Success();
    }
    #endregion

    #region Phone Number Validation
    /// <summary>
    /// 전화번호 유효성 검증
    /// </summary>
    public static ValidationResult ValidatePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return ValidationResult.Error("전화번호를 입력해주세요.", ValidationErrorType.Required, "phoneNumber");
        }

        // 공백 제거
        string cleanPhone = phoneNumber.Replace(" ", "").Replace("-", "");

        if (!PhonePattern.IsMatch(cleanPhone))
        {
            return ValidationResult.Error("올바른 전화번호 형식이 아닙니다. (예: 010-1234-5678)", 
                                        ValidationErrorType.Format, "phoneNumber");
        }

        return ValidationResult.Success();
    }
    #endregion

    #region Numeric Validation
    /// <summary>
    /// 숫자 범위 검증
    /// </summary>
    public static ValidationResult ValidateNumericRange(string value, int min, int max, string fieldName = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Error("값을 입력해주세요.", ValidationErrorType.Required, fieldName);
        }

        if (!int.TryParse(value, out int numericValue))
        {
            return ValidationResult.Error("숫자만 입력할 수 있습니다.", ValidationErrorType.Format, fieldName);
        }

        if (numericValue < min || numericValue > max)
        {
            return ValidationResult.Error($"값은 {min}부터 {max} 사이여야 합니다.", 
                                        ValidationErrorType.Range, fieldName);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 나이 검증
    /// </summary>
    public static ValidationResult ValidateAge(string age)
    {
        return ValidateNumericRange(age, 1, 120, "age");
    }

    /// <summary>
    /// 점수 검증
    /// </summary>
    public static ValidationResult ValidateScore(string score)
    {
        return ValidateNumericRange(score, 0, 999999, "score");
    }
    #endregion

    #region Text Content Validation
    /// <summary>
    /// 일반 텍스트 검증
    /// </summary>
    public static ValidationResult ValidateText(string text, int minLength = 1, int maxLength = 1000, 
                                              string fieldName = "", bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (allowEmpty)
                return ValidationResult.Success();
                
            return ValidationResult.Error("내용을 입력해주세요.", ValidationErrorType.Required, fieldName);
        }

        if (text.Length < minLength)
        {
            return ValidationResult.Error($"최소 {minLength}자 이상 입력해주세요.", 
                                        ValidationErrorType.Length, fieldName);
        }

        if (text.Length > maxLength)
        {
            return ValidationResult.Error($"최대 {maxLength}자까지 입력할 수 있습니다.", 
                                        ValidationErrorType.Length, fieldName);
        }

        // HTML/스크립트 태그 검증
        if (ContainsHtmlOrScript(text))
        {
            return ValidationResult.Error("HTML 태그나 스크립트는 입력할 수 없습니다.", 
                                        ValidationErrorType.Prohibited, fieldName);
        }

        // 금지된 단어 검증
        if (ContainsProhibitedWords(text))
        {
            return ValidationResult.Error("부적절한 내용이 포함되어 있습니다.", 
                                        ValidationErrorType.Prohibited, fieldName);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 댓글/메시지 검증
    /// </summary>
    public static ValidationResult ValidateComment(string comment)
    {
        return ValidateText(comment, 1, 500, "comment");
    }

    /// <summary>
    /// 게시글 제목 검증
    /// </summary>
    public static ValidationResult ValidateTitle(string title)
    {
        return ValidateText(title, 2, 100, "title");
    }
    #endregion

    #region URL Validation
    /// <summary>
    /// URL 검증
    /// </summary>
    public static ValidationResult ValidateUrl(string url, bool allowEmpty = true)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            if (allowEmpty)
                return ValidationResult.Success();
            
            return ValidationResult.Error("URL을 입력해주세요.", ValidationErrorType.Required, "url");
        }

        try
        {
            var uri = new Uri(url);
            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                return ValidationResult.Error("HTTP 또는 HTTPS URL만 허용됩니다.", 
                                            ValidationErrorType.Format, "url");
            }
        }
        catch (UriFormatException)
        {
            return ValidationResult.Error("올바른 URL 형식이 아닙니다.", ValidationErrorType.Format, "url");
        }

        return ValidationResult.Success();
    }
    #endregion

    #region File Validation
    /// <summary>
    /// 파일 이름 검증
    /// </summary>
    public static ValidationResult ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ValidationResult.Error("파일 이름을 입력해주세요.", ValidationErrorType.Required, "fileName");
        }

        // Windows 금지 문자
        char[] invalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        if (fileName.IndexOfAny(invalidChars) >= 0)
        {
            return ValidationResult.Error("파일 이름에 사용할 수 없는 문자가 포함되어 있습니다.", 
                                        ValidationErrorType.Pattern, "fileName");
        }

        // 예약된 이름
        string[] reservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "LPT1", "LPT2" };
        string upperFileName = fileName.ToUpper();
        if (Array.Exists(reservedNames, name => upperFileName.StartsWith(name)))
        {
            return ValidationResult.Error("시스템 예약어는 파일 이름으로 사용할 수 없습니다.", 
                                        ValidationErrorType.Prohibited, "fileName");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// 파일 확장자 검증
    /// </summary>
    public static ValidationResult ValidateFileExtension(string fileName, string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ValidationResult.Error("파일을 선택해주세요.", ValidationErrorType.Required, "file");
        }

        string extension = System.IO.Path.GetExtension(fileName)?.ToLower();
        if (string.IsNullOrEmpty(extension))
        {
            return ValidationResult.Error("파일 확장자가 없습니다.", ValidationErrorType.Format, "file");
        }

        if (!Array.Exists(allowedExtensions, ext => ext.ToLower() == extension))
        {
            string allowedList = string.Join(", ", allowedExtensions);
            return ValidationResult.Error($"허용된 파일 형식: {allowedList}", 
                                        ValidationErrorType.Format, "file");
        }

        return ValidationResult.Success();
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// 금지된 단어 포함 여부 확인
    /// </summary>
    private static bool ContainsProhibitedWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        foreach (string prohibited in ProhibitedWords)
        {
            if (text.Contains(prohibited, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 연속된 특수문자 확인
    /// </summary>
    private static bool HasConsecutiveSpecialChars(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        for (int i = 0; i < text.Length - 1; i++)
        {
            if ((text[i] == '_' || text[i] == '-') && 
                (text[i + 1] == '_' || text[i + 1] == '-'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 연속된 동일 문자 확인
    /// </summary>
    private static bool HasConsecutiveChars(string text, int maxConsecutive)
    {
        if (string.IsNullOrEmpty(text) || text.Length < maxConsecutive) return false;

        for (int i = 0; i <= text.Length - maxConsecutive; i++)
        {
            bool allSame = true;
            char firstChar = text[i];
            
            for (int j = 1; j < maxConsecutive; j++)
            {
                if (text[i + j] != firstChar)
                {
                    allSame = false;
                    break;
                }
            }
            
            if (allSame) return true;
        }

        return false;
    }

    /// <summary>
    /// HTML 또는 스크립트 태그 포함 여부 확인
    /// </summary>
    private static bool ContainsHtmlOrScript(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // 기본적인 HTML/스크립트 태그 검사
        string[] dangerousTags = { "<script", "<iframe", "<object", "<embed", "<form", 
                                 "javascript:", "vbscript:", "onload=", "onerror=" };

        string lowerText = text.ToLower();
        return Array.Exists(dangerousTags, tag => lowerText.Contains(tag));
    }

    /// <summary>
    /// 복수 필드 검증
    /// </summary>
    public static List<ValidationResult> ValidateMultiple(params Func<ValidationResult>[] validators)
    {
        var results = new List<ValidationResult>();
        
        foreach (var validator in validators)
        {
            try
            {
                var result = validator();
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(ValidationResult.Error($"검증 중 오류가 발생했습니다: {ex.Message}", 
                                                 ValidationErrorType.System));
            }
        }

        return results;
    }

    /// <summary>
    /// 검증 결과 중 첫 번째 오류 반환
    /// </summary>
    public static ValidationResult GetFirstError(params Func<ValidationResult>[] validators)
    {
        foreach (var validator in validators)
        {
            try
            {
                var result = validator();
                if (!result.IsValid)
                    return result;
            }
            catch (Exception ex)
            {
                return ValidationResult.Error($"검증 중 오류가 발생했습니다: {ex.Message}", 
                                            ValidationErrorType.System);
            }
        }

        return ValidationResult.Success();
    }
    #endregion

    #region Custom Validation Rules
    /// <summary>
    /// 커스텀 검증 규칙 적용
    /// </summary>
    public static ValidationResult ValidateCustom(string value, Func<string, bool> validationFunc, 
                                                 string errorMessage, string fieldName = "")
    {
        try
        {
            if (validationFunc(value))
                return ValidationResult.Success();
            else
                return ValidationResult.Error(errorMessage, ValidationErrorType.Pattern, fieldName);
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"검증 중 오류가 발생했습니다: {ex.Message}", 
                                        ValidationErrorType.System, fieldName);
        }
    }
    #endregion

    #region Integration with Error Handler
    /// <summary>
    /// 검증 오류를 GlobalErrorHandler로 전송
    /// </summary>
    public static void ReportValidationError(ValidationResult result, string context = "")
    {
        if (result.IsValid) return;

        GlobalErrorHandler.Instance.HandleValidationError(result.Field, result.ErrorMessage, context);
    }

    /// <summary>
    /// 복수 검증 결과의 오류들을 모두 리포트
    /// </summary>
    public static void ReportValidationErrors(List<ValidationResult> results, string context = "")
    {
        foreach (var result in results)
        {
            if (!result.IsValid)
            {
                ReportValidationError(result, context);
            }
        }
    }
    #endregion
}