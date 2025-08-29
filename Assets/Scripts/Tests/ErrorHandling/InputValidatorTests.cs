using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// InputValidator에 대한 단위 테스트
/// 모든 검증 규칙과 엣지 케이스를 포괄적으로 테스트
/// </summary>
public class InputValidatorTests
{
    #region Nickname Validation Tests
    [Test]
    public void ValidateNickname_ValidInput_ShouldReturnSuccess()
    {
        // Arrange
        string[] validNicknames = { "테스트", "Test123", "한글English", "닉네임_123", "user-name" };
        
        foreach (string nickname in validNicknames)
        {
            // Act
            var result = InputValidator.ValidateNickname(nickname);
            
            // Assert
            Assert.IsTrue(result.IsValid, $"'{nickname}' should be valid but got: {result.ErrorMessage}");
        }
    }
    
    [Test]
    public void ValidateNickname_NullOrEmpty_ShouldReturnError()
    {
        // Arrange
        string[] invalidNicknames = { null, "", "   ", "\t\n" };
        
        foreach (string nickname in invalidNicknames)
        {
            // Act
            var result = InputValidator.ValidateNickname(nickname);
            
            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(InputValidator.ValidationErrorType.Required, result.ErrorType);
            Assert.AreEqual("nickname", result.Field);
        }
    }
    
    [Test]
    public void ValidateNickname_TooShort_ShouldReturnError()
    {
        // Arrange
        string shortNickname = "a";
        
        // Act
        var result = InputValidator.ValidateNickname(shortNickname);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Length, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("최소 2자"));
    }
    
    [Test]
    public void ValidateNickname_TooLong_ShouldReturnError()
    {
        // Arrange
        string longNickname = "VeryLongNicknameExceedingMaximumLength";
        
        // Act
        var result = InputValidator.ValidateNickname(longNickname);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Length, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("최대 12자"));
    }
    
    [Test]
    public void ValidateNickname_InvalidCharacters_ShouldReturnError()
    {
        // Arrange
        string[] invalidNicknames = { "nick@name", "user#123", "test&user", "name%test", "user*name" };
        
        foreach (string nickname in invalidNicknames)
        {
            // Act
            var result = InputValidator.ValidateNickname(nickname);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{nickname}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateNickname_ProhibitedWords_ShouldReturnError()
    {
        // Arrange
        string[] prohibitedNicknames = { "admin", "관리자", "test", "system" };
        
        foreach (string nickname in prohibitedNicknames)
        {
            // Act
            var result = InputValidator.ValidateNickname(nickname);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{nickname}' should be prohibited");
            Assert.AreEqual(InputValidator.ValidationErrorType.Prohibited, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateNickname_ConsecutiveSpecialChars_ShouldReturnError()
    {
        // Arrange
        string[] invalidNicknames = { "user__name", "test--user", "nick-_test" };
        
        foreach (string nickname in invalidNicknames)
        {
            // Act
            var result = InputValidator.ValidateNickname(nickname);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{nickname}' should be invalid due to consecutive special chars");
            Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateNickname_StartOrEndWithSpecialChar_ShouldReturnError()
    {
        // Arrange
        string[] invalidNicknames = { "_username", "username_", "-nickname", "nickname-" };
        
        foreach (string nickname in invalidNicknames)
        {
            // Act
            var result = InputValidator.ValidateNickname(nickname);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{nickname}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
            Assert.IsTrue(result.ErrorMessage.Contains("특수문자로 시작하거나 끝날 수 없습니다"));
        }
    }
    #endregion
    
    #region Email Validation Tests
    [Test]
    public void ValidateEmail_ValidEmails_ShouldReturnSuccess()
    {
        // Arrange
        string[] validEmails = { 
            "test@example.com", 
            "user123@domain.co.kr", 
            "name.surname@company.org",
            "user+tag@domain.net"
        };
        
        foreach (string email in validEmails)
        {
            // Act
            var result = InputValidator.ValidateEmail(email);
            
            // Assert
            Assert.IsTrue(result.IsValid, $"'{email}' should be valid but got: {result.ErrorMessage}");
        }
    }
    
    [Test]
    public void ValidateEmail_InvalidFormat_ShouldReturnError()
    {
        // Arrange
        string[] invalidEmails = { 
            "invalid-email", 
            "@domain.com", 
            "user@", 
            "user@domain", 
            "user space@domain.com",
            "user..name@domain.com"
        };
        
        foreach (string email in invalidEmails)
        {
            // Act
            var result = InputValidator.ValidateEmail(email);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{email}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Format, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateEmail_TooLong_ShouldReturnError()
    {
        // Arrange
        string longEmail = new string('a', 90) + "@domain.com"; // > 100 chars
        
        // Act
        var result = InputValidator.ValidateEmail(longEmail);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Length, result.ErrorType);
    }
    #endregion
    
    #region Password Validation Tests
    [Test]
    public void ValidatePassword_ValidPasswords_ShouldReturnSuccess()
    {
        // Arrange
        string[] validPasswords = { 
            "Password123!",
            "MySecure@Pass1",
            "Str0ng&P@ssw0rd",
            "Valid123#"
        };
        
        foreach (string password in validPasswords)
        {
            // Act
            var result = InputValidator.ValidatePassword(password);
            
            // Assert
            Assert.IsTrue(result.IsValid, $"'{password}' should be valid but got: {result.ErrorMessage}");
        }
    }
    
    [Test]
    public void ValidatePassword_TooShort_ShouldReturnError()
    {
        // Arrange
        string shortPassword = "Pass1!";
        
        // Act
        var result = InputValidator.ValidatePassword(shortPassword);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Length, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("최소 8자"));
    }
    
    [Test]
    public void ValidatePassword_MissingUppercase_ShouldReturnError()
    {
        // Arrange
        string password = "password123!";
        
        // Act
        var result = InputValidator.ValidatePassword(password);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("대문자"));
    }
    
    [Test]
    public void ValidatePassword_MissingLowercase_ShouldReturnError()
    {
        // Arrange
        string password = "PASSWORD123!";
        
        // Act
        var result = InputValidator.ValidatePassword(password);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("소문자"));
    }
    
    [Test]
    public void ValidatePassword_MissingDigit_ShouldReturnError()
    {
        // Arrange
        string password = "Password!";
        
        // Act
        var result = InputValidator.ValidatePassword(password);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("숫자"));
    }
    
    [Test]
    public void ValidatePassword_MissingSpecialChar_ShouldReturnError()
    {
        // Arrange
        string password = "Password123";
        
        // Act
        var result = InputValidator.ValidatePassword(password);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("특수문자"));
    }
    
    [Test]
    public void ValidatePassword_ConsecutiveChars_ShouldReturnError()
    {
        // Arrange
        string password = "Passsss123!";
        
        // Act
        var result = InputValidator.ValidatePassword(password);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("3회 이상 연속"));
    }
    
    [Test]
    public void ValidatePasswordConfirm_Matching_ShouldReturnSuccess()
    {
        // Arrange
        string password = "Password123!";
        string confirmPassword = "Password123!";
        
        // Act
        var result = InputValidator.ValidatePasswordConfirm(password, confirmPassword);
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    
    [Test]
    public void ValidatePasswordConfirm_NotMatching_ShouldReturnError()
    {
        // Arrange
        string password = "Password123!";
        string confirmPassword = "Different123!";
        
        // Act
        var result = InputValidator.ValidatePasswordConfirm(password, confirmPassword);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("일치하지 않습니다"));
    }
    #endregion
    
    #region Phone Number Validation Tests
    [Test]
    public void ValidatePhoneNumber_ValidFormats_ShouldReturnSuccess()
    {
        // Arrange
        string[] validPhoneNumbers = {
            "010-1234-5678",
            "01012345678",
            "010 1234 5678",
            "016-123-4567",
            "017-1234-5678"
        };
        
        foreach (string phoneNumber in validPhoneNumbers)
        {
            // Act
            var result = InputValidator.ValidatePhoneNumber(phoneNumber);
            
            // Assert
            Assert.IsTrue(result.IsValid, $"'{phoneNumber}' should be valid but got: {result.ErrorMessage}");
        }
    }
    
    [Test]
    public void ValidatePhoneNumber_InvalidFormats_ShouldReturnError()
    {
        // Arrange
        string[] invalidPhoneNumbers = {
            "02-1234-5678",  // 지역번호
            "010-12-5678",   // 잘못된 자릿수
            "010-12345-678", // 잘못된 자릿수
            "01-1234-5678",  // 잘못된 시작번호
            "phone number"   // 문자
        };
        
        foreach (string phoneNumber in invalidPhoneNumbers)
        {
            // Act
            var result = InputValidator.ValidatePhoneNumber(phoneNumber);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{phoneNumber}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Format, result.ErrorType);
        }
    }
    #endregion
    
    #region Numeric Validation Tests
    [Test]
    public void ValidateNumericRange_ValidRange_ShouldReturnSuccess()
    {
        // Act & Assert
        Assert.IsTrue(InputValidator.ValidateNumericRange("50", 1, 100).IsValid);
        Assert.IsTrue(InputValidator.ValidateNumericRange("1", 1, 100).IsValid);
        Assert.IsTrue(InputValidator.ValidateNumericRange("100", 1, 100).IsValid);
    }
    
    [Test]
    public void ValidateNumericRange_OutOfRange_ShouldReturnError()
    {
        // Act & Assert
        var belowRange = InputValidator.ValidateNumericRange("0", 1, 100);
        var aboveRange = InputValidator.ValidateNumericRange("101", 1, 100);
        
        Assert.IsFalse(belowRange.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Range, belowRange.ErrorType);
        Assert.IsFalse(aboveRange.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Range, aboveRange.ErrorType);
    }
    
    [Test]
    public void ValidateNumericRange_NonNumeric_ShouldReturnError()
    {
        // Act
        var result = InputValidator.ValidateNumericRange("not a number", 1, 100);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Format, result.ErrorType);
    }
    
    [Test]
    public void ValidateAge_ValidAge_ShouldReturnSuccess()
    {
        // Act & Assert
        Assert.IsTrue(InputValidator.ValidateAge("25").IsValid);
        Assert.IsTrue(InputValidator.ValidateAge("1").IsValid);
        Assert.IsTrue(InputValidator.ValidateAge("120").IsValid);
    }
    
    [Test]
    public void ValidateAge_InvalidAge_ShouldReturnError()
    {
        // Act & Assert
        Assert.IsFalse(InputValidator.ValidateAge("0").IsValid);
        Assert.IsFalse(InputValidator.ValidateAge("121").IsValid);
        Assert.IsFalse(InputValidator.ValidateAge("-5").IsValid);
    }
    #endregion
    
    #region Text Content Validation Tests
    [Test]
    public void ValidateText_ValidText_ShouldReturnSuccess()
    {
        // Arrange
        string validText = "This is a valid text content.";
        
        // Act
        var result = InputValidator.ValidateText(validText, 1, 100);
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    
    [Test]
    public void ValidateText_WithHtmlTags_ShouldReturnError()
    {
        // Arrange
        string[] invalidTexts = {
            "<script>alert('xss');</script>",
            "<iframe src='malicious.com'></iframe>",
            "javascript:alert('hack')",
            "<object data='malicious.swf'></object>"
        };
        
        foreach (string text in invalidTexts)
        {
            // Act
            var result = InputValidator.ValidateText(text, 1, 1000);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{text}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Prohibited, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateText_WithProhibitedWords_ShouldReturnError()
    {
        // Arrange
        string textWithProfanity = "This text contains bad words like 씨발 in it.";
        
        // Act
        var result = InputValidator.ValidateText(textWithProfanity, 1, 1000);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Prohibited, result.ErrorType);
    }
    
    [Test]
    public void ValidateComment_ValidComment_ShouldReturnSuccess()
    {
        // Arrange
        string validComment = "This is a valid comment.";
        
        // Act
        var result = InputValidator.ValidateComment(validComment);
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    
    [Test]
    public void ValidateTitle_ValidTitle_ShouldReturnSuccess()
    {
        // Arrange
        string validTitle = "Valid Title";
        
        // Act
        var result = InputValidator.ValidateTitle(validTitle);
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    #endregion
    
    #region URL Validation Tests
    [Test]
    public void ValidateUrl_ValidUrls_ShouldReturnSuccess()
    {
        // Arrange
        string[] validUrls = {
            "https://www.example.com",
            "http://subdomain.example.co.kr",
            "https://example.com/path/to/resource",
            "https://example.com:8080/api/v1"
        };
        
        foreach (string url in validUrls)
        {
            // Act
            var result = InputValidator.ValidateUrl(url);
            
            // Assert
            Assert.IsTrue(result.IsValid, $"'{url}' should be valid but got: {result.ErrorMessage}");
        }
    }
    
    [Test]
    public void ValidateUrl_InvalidUrls_ShouldReturnError()
    {
        // Arrange
        string[] invalidUrls = {
            "ftp://example.com",
            "not-a-url",
            "javascript:alert('xss')",
            "//example.com"
        };
        
        foreach (string url in invalidUrls)
        {
            // Act
            var result = InputValidator.ValidateUrl(url, false); // allowEmpty = false
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{url}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Format, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateUrl_EmptyWithAllowEmpty_ShouldReturnSuccess()
    {
        // Act
        var result = InputValidator.ValidateUrl("", true);
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    #endregion
    
    #region File Validation Tests
    [Test]
    public void ValidateFileName_ValidNames_ShouldReturnSuccess()
    {
        // Arrange
        string[] validNames = {
            "document.txt",
            "이미지_파일.jpg",
            "data-file_v2.csv",
            "report (final).pdf"
        };
        
        foreach (string fileName in validNames)
        {
            // Act
            var result = InputValidator.ValidateFileName(fileName);
            
            // Assert
            Assert.IsTrue(result.IsValid, $"'{fileName}' should be valid but got: {result.ErrorMessage}");
        }
    }
    
    [Test]
    public void ValidateFileName_InvalidCharacters_ShouldReturnError()
    {
        // Arrange
        string[] invalidNames = {
            "file<name.txt",
            "file>name.txt",
            "file:name.txt",
            "file\"name.txt",
            "file/name.txt",
            "file\\name.txt",
            "file|name.txt",
            "file?name.txt",
            "file*name.txt"
        };
        
        foreach (string fileName in invalidNames)
        {
            // Act
            var result = InputValidator.ValidateFileName(fileName);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{fileName}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateFileName_ReservedNames_ShouldReturnError()
    {
        // Arrange
        string[] reservedNames = { "CON.txt", "PRN.log", "AUX.data", "NUL.file" };
        
        foreach (string fileName in reservedNames)
        {
            // Act
            var result = InputValidator.ValidateFileName(fileName);
            
            // Assert
            Assert.IsFalse(result.IsValid, $"'{fileName}' should be invalid");
            Assert.AreEqual(InputValidator.ValidationErrorType.Prohibited, result.ErrorType);
        }
    }
    
    [Test]
    public void ValidateFileExtension_AllowedExtensions_ShouldReturnSuccess()
    {
        // Arrange
        string[] allowedExtensions = { ".txt", ".jpg", ".png", ".pdf" };
        
        // Act & Assert
        Assert.IsTrue(InputValidator.ValidateFileExtension("document.txt", allowedExtensions).IsValid);
        Assert.IsTrue(InputValidator.ValidateFileExtension("image.JPG", allowedExtensions).IsValid);
        Assert.IsTrue(InputValidator.ValidateFileExtension("photo.png", allowedExtensions).IsValid);
    }
    
    [Test]
    public void ValidateFileExtension_DisallowedExtensions_ShouldReturnError()
    {
        // Arrange
        string[] allowedExtensions = { ".txt", ".jpg" };
        
        // Act
        var result = InputValidator.ValidateFileExtension("document.pdf", allowedExtensions);
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Format, result.ErrorType);
    }
    #endregion
    
    #region Multiple Validation Tests
    [Test]
    public void ValidateMultiple_AllValid_ShouldReturnAllSuccess()
    {
        // Act
        var results = InputValidator.ValidateMultiple(
            () => InputValidator.ValidateNickname("validNick"),
            () => InputValidator.ValidateEmail("test@example.com"),
            () => InputValidator.ValidateAge("25")
        );
        
        // Assert
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.TrueForAll(r => r.IsValid));
    }
    
    [Test]
    public void ValidateMultiple_SomeInvalid_ShouldReturnMixedResults()
    {
        // Act
        var results = InputValidator.ValidateMultiple(
            () => InputValidator.ValidateNickname("validNick"),
            () => InputValidator.ValidateEmail("invalid-email"),
            () => InputValidator.ValidateAge("150")
        );
        
        // Assert
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].IsValid);
        Assert.IsFalse(results[1].IsValid);
        Assert.IsFalse(results[2].IsValid);
    }
    
    [Test]
    public void GetFirstError_WithErrors_ShouldReturnFirstError()
    {
        // Act
        var result = InputValidator.GetFirstError(
            () => InputValidator.ValidateNickname("validNick"),
            () => InputValidator.ValidateEmail("invalid-email"),
            () => InputValidator.ValidateAge("150")
        );
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.Format, result.ErrorType);
        Assert.AreEqual("email", result.Field);
    }
    
    [Test]
    public void GetFirstError_AllValid_ShouldReturnSuccess()
    {
        // Act
        var result = InputValidator.GetFirstError(
            () => InputValidator.ValidateNickname("validNick"),
            () => InputValidator.ValidateEmail("test@example.com"),
            () => InputValidator.ValidateAge("25")
        );
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    #endregion
    
    #region Custom Validation Tests
    [Test]
    public void ValidateCustom_PassingValidation_ShouldReturnSuccess()
    {
        // Act
        var result = InputValidator.ValidateCustom("test", 
            value => value.Length > 2, 
            "Value must be longer than 2 characters");
        
        // Assert
        Assert.IsTrue(result.IsValid);
    }
    
    [Test]
    public void ValidateCustom_FailingValidation_ShouldReturnError()
    {
        // Act
        var result = InputValidator.ValidateCustom("ab", 
            value => value.Length > 2, 
            "Value must be longer than 2 characters");
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Value must be longer than 2 characters", result.ErrorMessage);
        Assert.AreEqual(InputValidator.ValidationErrorType.Pattern, result.ErrorType);
    }
    
    [Test]
    public void ValidateCustom_ExceptionInValidation_ShouldReturnSystemError()
    {
        // Act
        var result = InputValidator.ValidateCustom("test", 
            value => throw new InvalidOperationException("Test exception"), 
            "Custom error message");
        
        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(InputValidator.ValidationErrorType.System, result.ErrorType);
        Assert.IsTrue(result.ErrorMessage.Contains("검증 중 오류가 발생했습니다"));
    }
    #endregion
}