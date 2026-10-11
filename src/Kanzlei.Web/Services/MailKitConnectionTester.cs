using System.Net.Sockets;
using Kanzlei.Web.Data;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Kanzlei.Web.Services;

// Prüft nur Verbindung und Anmeldung; es wird keine E-Mail versendet. Exception-Meldungen werden nie ausgegeben oder protokolliert.
public sealed class MailKitConnectionTester : IMailConnectionTester
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<MailKitConnectionTester> _logger;
    public MailKitConnectionTester(ILogger<MailKitConnectionTester> logger) => _logger = logger;

    public async Task<MailTestResult> TestAsync(MailSettings s, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds, CheckCertificateRevocation = false };
            var options = s.Tls switch
            {
                MailSslModus.SslBeiVerbindung => SecureSocketOptions.SslOnConnect,
                MailSslModus.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.None,
            };
            await client.ConnectAsync(s.Host, s.Port, options, timeout.Token);
            if (s.HasCredentials) await client.AuthenticateAsync(s.UserName!, s.Password!, timeout.Token);
            await client.DisconnectAsync(true, CancellationToken.None);
            return new MailTestResult(MailTestOutcome.Ok);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var outcome = ex switch
            {
                OperationCanceledException or TimeoutException => MailTestOutcome.Timeout,
                AuthenticationException => MailTestOutcome.AuthenticationFailed,
                SslHandshakeException or System.Security.Authentication.AuthenticationException => MailTestOutcome.TlsFailed,
                SocketException or IOException or ServiceNotConnectedException => MailTestOutcome.ConnectionFailed,
                _ => MailTestOutcome.Other,
            };
            _logger.LogWarning("SMTP-Test fehlgeschlagen ({Outcome}, {ExceptionType}).", outcome, ex.GetType().Name);
            return new MailTestResult(outcome);
        }
    }
}
