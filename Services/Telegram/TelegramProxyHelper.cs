using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MARS.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace MARS.Server.Services.Telegram;

internal static class TelegramProxyHelper
{
    internal static Uri? ResolveHttpProxyUri(string proxyValue, string? forceType = null)
    {
        Uri? result = null;

        if (!string.IsNullOrWhiteSpace(forceType) && !forceType.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            result = ForceProxyType(proxyValue, forceType);
        }
        else if (TryParseTelegramSocksLink(proxyValue, out var socksUriFromLink))
        {
            result = socksUriFromLink;
        }
        else if (TryParseProxyUri(proxyValue, out var proxyUri))
        {
            result = proxyUri;
        }

        return result;
    }

    private static Uri? ForceProxyType(string proxyValue, string forceType)
    {
        Uri? result = null;

        var normalized = proxyValue.Trim();

        if (TryParseTelegramSocksLink(normalized, out var socksUri))
        {
            normalized = $"socks5://{socksUri.UserInfo}@{socksUri.Host}:{socksUri.Port}";
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var parsedUri) && parsedUri is not null)
        {
            var targetScheme = forceType.Equals("socks5", StringComparison.OrdinalIgnoreCase) ? "socks5" : "http";
            var userInfo = string.IsNullOrWhiteSpace(parsedUri.UserInfo)
                ? string.Empty
                : $"{parsedUri.UserInfo}@";
            var rebuilt = $"{targetScheme}://{userInfo}{parsedUri.Host}:{parsedUri.Port}";

            if (Uri.TryCreate(rebuilt, UriKind.Absolute, out var forcedUri))
            {
                result = forcedUri;
            }
        }

        return result;
    }

    internal static Uri? ReadBotProxyUri(TelegramProxyConfiguration config, ILogger logger)
    {
        Uri? result = null;

        try
        {
            var proxyValue = !string.IsNullOrWhiteSpace(config.BotProxyUrl)
                ? config.BotProxyUrl.Trim()
                : !string.IsNullOrWhiteSpace(config.WTelegramProxyUrl)
                    ? config.WTelegramProxyUrl.Trim()
                    : null;

            if (!string.IsNullOrWhiteSpace(proxyValue))
            {
                if (IsMtProxyUrl(proxyValue) || TryParseTelegramProxyLink(proxyValue, out _))
                {
                    logger.LogWarning(
                        "MTProxy URL обнаружен в конфигурации для Telegram Bot API, но MTProxy не поддерживается Bot HTTP API. Прокси игнорируется: {Value}",
                        proxyValue
                    );
                }
                else
                {
                    var forceType = NormalizeProxyType(config.BotProxyType);
                    result = ResolveHttpProxyUri(proxyValue, forceType);

                    if (result is null)
                    {
                        logger.LogWarning(
                            "Некорректный формат прокси в конфигурации для Telegram Bot API. Ожидается socks5://, http:// или t.me/socks"
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Не удалось прочитать настройки прокси из конфигурации для Telegram Bot API. Клиент продолжит работу без прокси"
            );
        }

        return result;
    }

    internal static string? ReadWTelegramProxyType(TelegramProxyConfiguration config)
    {
        return NormalizeProxyType(config.WTelegramProxyType);
    }

    private static string? NormalizeProxyType(string? proxyType)
    {
        if (!string.IsNullOrWhiteSpace(proxyType))
        {
            var value = proxyType.Trim();
            if (!value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    internal static bool IsMtProxyUrl(string proxyValue)
    {
        return proxyValue.Contains("/proxy?", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryParseProxyUri(string proxyValue, out Uri proxyUri)
    {
        var isValidUri = Uri.TryCreate(proxyValue, UriKind.Absolute, out var parsedUri);
        if (
            isValidUri
            && parsedUri is not null
            && (
                parsedUri.Scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase)
                || parsedUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            proxyUri = parsedUri;
            return true;
        }

        proxyUri = null!;
        return false;
    }

    internal static bool TryParseTelegramProxyLink(string proxyValue, out string mtProxyUrl)
    {
        mtProxyUrl = string.Empty;

        if (!Uri.TryCreate(proxyValue, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!uri.AbsolutePath.Trim('/').Equals("proxy", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQueryString(uri.Query);
        if (
            !query.TryGetValue("server", out var server)
            || !query.TryGetValue("port", out var portRaw)
            || !query.TryGetValue("secret", out var secret)
            || string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(portRaw)
            || string.IsNullOrWhiteSpace(secret)
            || !int.TryParse(portRaw, out var port)
            || port <= 0
            || port > 65535
        )
        {
            return false;
        }

        mtProxyUrl =
            $"https://t.me/proxy?server={Uri.EscapeDataString(server)}&port={port}&secret={Uri.EscapeDataString(secret)}";
        return true;
    }

    internal static bool TryParseTelegramSocksLink(string proxyValue, out Uri proxyUri)
    {
        proxyUri = null!;

        if (!Uri.TryCreate(proxyValue, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("t.me", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!uri.AbsolutePath.Trim('/').Equals("socks", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = ParseQueryString(uri.Query);
        if (
            !query.TryGetValue("server", out var server)
            || !query.TryGetValue("port", out var portRaw)
            || string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(portRaw)
            || !int.TryParse(portRaw, out var port)
            || port <= 0
            || port > 65535
        )
        {
            return false;
        }

        query.TryGetValue("user", out var user);
        query.TryGetValue("pass", out var pass);

        var userInfo = string.IsNullOrWhiteSpace(user)
            ? string.Empty
            : $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass ?? string.Empty)}@";

        var normalizedProxy = $"socks5://{userInfo}{server}:{port}";
        var isParsed = Uri.TryCreate(normalizedProxy, UriKind.Absolute, out var parsedProxyUri);
        if (isParsed && parsedProxyUri is not null)
        {
            proxyUri = parsedProxyUri;
            return true;
        }

        return false;
    }

    internal static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return result;
        }

        var query = queryString.StartsWith('?') ? queryString[1..] : queryString;
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
            var value = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var decodedKey = Uri.UnescapeDataString(key.Replace('+', ' '));
            var decodedValue = Uri.UnescapeDataString(value.Replace('+', ' '));
            result[decodedKey] = decodedValue;
        }

        return result;
    }

    internal static async Task<TcpClient> ConnectThroughSocks5ProxyAsync(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort,
        CancellationToken cancellationToken = default
    )
    {
        var tcpClient = new TcpClient();
        try
        {
            var proxyPort = proxyUri.IsDefaultPort ? 1080 : proxyUri.Port;
            await tcpClient.ConnectAsync(proxyUri.Host, proxyPort, cancellationToken);

            await using var stream = tcpClient.GetStream();

            var hasCredentials = !string.IsNullOrWhiteSpace(proxyUri.UserInfo);
            var greeting = hasCredentials
                ? new byte[] { 0x05, 0x02, 0x00, 0x02 }
                : new byte[] { 0x05, 0x01, 0x00 };
            await stream.WriteAsync(greeting, cancellationToken);

            var methodResponse = await ReadExactAsync(stream, 2, cancellationToken);
            if (methodResponse[0] != 0x05)
            {
                throw new InvalidOperationException("Неверный ответ SOCKS5 прокси");
            }

            if (methodResponse[1] == 0xFF)
            {
                throw new InvalidOperationException(
                    "SOCKS5 прокси не поддерживает доступные методы аутентификации"
                );
            }

            if (methodResponse[1] == 0x02)
            {
                await AuthenticateSocks5Async(stream, proxyUri, cancellationToken);
            }
            else if (methodResponse[1] != 0x00)
            {
                throw new InvalidOperationException(
                    $"SOCKS5 прокси вернул неподдерживаемый метод: {methodResponse[1]}"
                );
            }

            var connectRequest = BuildSocks5ConnectRequest(destinationAddress, destinationPort);
            await stream.WriteAsync(connectRequest, cancellationToken);

            var connectResponseHead = await ReadExactAsync(stream, 4, cancellationToken);
            if (connectResponseHead[0] != 0x05)
            {
                throw new InvalidOperationException("Неверный ответ SOCKS5 при CONNECT");
            }

            if (connectResponseHead[1] != 0x00)
            {
                throw new InvalidOperationException(
                    $"SOCKS5 CONNECT завершился ошибкой: {connectResponseHead[1]}"
                );
            }

            await ConsumeSocks5BindAddressAsync(stream, connectResponseHead[3], cancellationToken);

            return tcpClient;
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }

    internal static async Task<TcpClient> ConnectThroughHttpProxyAsync(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort,
        CancellationToken cancellationToken = default
    )
    {
        var tcpClient = new TcpClient();
        try
        {
            var proxyPort = proxyUri.IsDefaultPort ? 80 : proxyUri.Port;
            await tcpClient.ConnectAsync(proxyUri.Host, proxyPort, cancellationToken);

            await using var stream = tcpClient.GetStream();

            var connectRequest = BuildHttpConnectRequest(
                proxyUri,
                destinationAddress,
                destinationPort
            );
            var requestBytes = Encoding.ASCII.GetBytes(connectRequest);
            await stream.WriteAsync(requestBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            var responseHeaders = await ReadHttpResponseHeadersAsync(stream, cancellationToken);
            if (
                !responseHeaders.StartsWith("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase)
                && !responseHeaders.StartsWith("HTTP/1.0 200", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    $"HTTP proxy CONNECT отклонен. Ответ прокси: {responseHeaders.Split("\r\n")[0]}"
                );
            }

            return tcpClient;
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }
    }

    private static string BuildHttpConnectRequest(
        Uri proxyUri,
        string destinationAddress,
        int destinationPort
    )
    {
        var destination = $"{destinationAddress}:{destinationPort}";
        var authHeader = string.Empty;

        if (!string.IsNullOrWhiteSpace(proxyUri.UserInfo))
        {
            var credentials = Uri.UnescapeDataString(proxyUri.UserInfo);
            var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            authHeader = $"Proxy-Authorization: Basic {encodedCredentials}\r\n";
        }

        return $"CONNECT {destination} HTTP/1.1\r\nHost: {destination}\r\nProxy-Connection: Keep-Alive\r\n{authHeader}\r\n";
    }

    private static async Task<string> ReadHttpResponseHeadersAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[4096];
        var responseBuffer = new List<byte>(4096);

        while (true)
        {
            var readCount = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (readCount == 0)
            {
                throw new IOException("HTTP proxy закрыл соединение во время CONNECT");
            }

            responseBuffer.AddRange(buffer.AsSpan(0, readCount).ToArray());

            if (responseBuffer.Count >= 4)
            {
                var count = responseBuffer.Count;
                if (
                    responseBuffer[count - 4] == '\r'
                    && responseBuffer[count - 3] == '\n'
                    && responseBuffer[count - 2] == '\r'
                    && responseBuffer[count - 1] == '\n'
                )
                {
                    return Encoding.ASCII.GetString(responseBuffer.ToArray());
                }
            }

            if (responseBuffer.Count > 64 * 1024)
            {
                throw new InvalidOperationException("Слишком длинный HTTP-ответ от прокси");
            }
        }
    }

    private static async Task AuthenticateSocks5Async(
        NetworkStream stream,
        Uri proxyUri,
        CancellationToken cancellationToken
    )
    {
        var credentials = Uri.UnescapeDataString(proxyUri.UserInfo ?? string.Empty);
        var separatorIndex = credentials.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException(
                "Для SOCKS5-аутентификации требуется формат user:password"
            );
        }

        var username = credentials[..separatorIndex];
        var password = credentials[(separatorIndex + 1)..];
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        if (usernameBytes.Length > byte.MaxValue || passwordBytes.Length > byte.MaxValue)
        {
            throw new InvalidOperationException(
                "Логин или пароль SOCKS5 превышают максимально допустимую длину"
            );
        }

        var authRequest = new byte[3 + usernameBytes.Length + passwordBytes.Length];
        authRequest[0] = 0x01;
        authRequest[1] = (byte)usernameBytes.Length;
        Buffer.BlockCopy(usernameBytes, 0, authRequest, 2, usernameBytes.Length);
        authRequest[2 + usernameBytes.Length] = (byte)passwordBytes.Length;
        Buffer.BlockCopy(
            passwordBytes,
            0,
            authRequest,
            3 + usernameBytes.Length,
            passwordBytes.Length
        );

        await stream.WriteAsync(authRequest, cancellationToken);

        var authResponse = await ReadExactAsync(stream, 2, cancellationToken);
        if (authResponse[1] != 0x00)
        {
            throw new InvalidOperationException("SOCKS5-аутентификация отклонена прокси");
        }
    }

    private static byte[] BuildSocks5ConnectRequest(string destinationAddress, int destinationPort)
    {
        var portBytes = new[]
        {
            (byte)((destinationPort >> 8) & 0xFF),
            (byte)(destinationPort & 0xFF),
        };

        if (IPAddress.TryParse(destinationAddress, out var ipAddress))
        {
            var ipBytes = ipAddress.GetAddressBytes();
            var addressType = ipBytes.Length == 16 ? (byte)0x04 : (byte)0x01;
            var request = new byte[4 + ipBytes.Length + 2];
            request[0] = 0x05;
            request[1] = 0x01;
            request[2] = 0x00;
            request[3] = addressType;
            Buffer.BlockCopy(ipBytes, 0, request, 4, ipBytes.Length);
            Buffer.BlockCopy(portBytes, 0, request, 4 + ipBytes.Length, 2);
            return request;
        }

        var hostBytes = Encoding.ASCII.GetBytes(destinationAddress);
        if (hostBytes.Length == 0 || hostBytes.Length > byte.MaxValue)
        {
            throw new InvalidOperationException("Некорректный адрес назначения для SOCKS5 CONNECT");
        }

        var domainRequest = new byte[5 + hostBytes.Length + 2];
        domainRequest[0] = 0x05;
        domainRequest[1] = 0x01;
        domainRequest[2] = 0x00;
        domainRequest[3] = 0x03;
        domainRequest[4] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, domainRequest, 5, hostBytes.Length);
        Buffer.BlockCopy(portBytes, 0, domainRequest, 5 + hostBytes.Length, 2);
        return domainRequest;
    }

    private static async Task ConsumeSocks5BindAddressAsync(
        NetworkStream stream,
        byte addressType,
        CancellationToken cancellationToken
    )
    {
        var addressLength = addressType switch
        {
            0x01 => 4,
            0x04 => 16,
            0x03 => (await ReadExactAsync(stream, 1, cancellationToken))[0],
            _ => throw new InvalidOperationException(
                $"Неизвестный тип адреса SOCKS5: {addressType}"
            ),
        };

        await ReadExactAsync(stream, addressLength + 2, cancellationToken);
    }

    private static async Task<byte[]> ReadExactAsync(
        NetworkStream stream,
        int count,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[count];
        var offset = 0;

        while (offset < count)
        {
            var readCount = await stream.ReadAsync(
                buffer,
                offset,
                count - offset,
                cancellationToken
            );
            if (readCount == 0)
            {
                throw new IOException(
                    $"SOCKS5 прокси закрыл соединение. Прочитано {offset} из {count} байт"
                );
            }

            offset += readCount;
        }

        return buffer;
    }
}
