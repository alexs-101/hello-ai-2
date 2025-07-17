namespace Core.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public string ErrorMessage => string.Join("; ", Errors);
}