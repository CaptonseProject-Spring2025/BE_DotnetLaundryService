using System;

namespace LaundryService.Dto.Responses;

public class AIResponse
{
  public bool Success { get; set; }
  public string Response { get; set; }
  public string Error { get; set; }
}
