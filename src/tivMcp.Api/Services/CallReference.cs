using System.Security.Cryptography;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Services;

/// <inheritdoc />
public sealed class CallReference : ICallReference
{
  /// <inheritdoc />
  public string Value { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
