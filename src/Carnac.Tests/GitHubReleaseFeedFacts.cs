using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>
    /// Runs the real feed against a tiny HTTP server on the loopback interface of this computer.
    /// Nothing leaves the machine and nothing talks to GitHub.
    /// </summary>
    public class GitHubReleaseFeedFacts : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        Thread serverThread;
        string receivedRequest;

        public void Dispose()
        {
            stop.Set();
            listener.Stop();
            if (serverThread != null)
                serverThread.Join(2000);
            stop.Dispose();
        }

        Uri StartServer(Action<NetworkStream> respond)
        {
            listener.Start();
            serverThread = new Thread(() =>
            {
                try
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        receivedRequest = ReadRequestHead(stream);
                        respond(stream);
                    }
                }
                catch (SocketException)
                {
                    // the listener was stopped while waiting
                }
                catch (IOException)
                {
                    // the client gave up (timeout test)
                }
                catch (InvalidOperationException)
                {
                    // the listener was stopped while waiting
                }
            });
            serverThread.IsBackground = true;
            serverThread.Start();
            return new Uri("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/repos/doggy8088/carnac/releases/latest");
        }

        static string ReadRequestHead(NetworkStream stream)
        {
            var head = new StringBuilder();
            var buffer = new byte[1];
            while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && stream.Read(buffer, 0, 1) == 1)
                head.Append((char)buffer[0]);
            return head.ToString();
        }

        static void Send(NetworkStream stream, string status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var head = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        [Fact]
        public void returns_the_body_of_a_successful_answer()
        {
            var url = StartServer(stream => Send(stream, "200 OK", @"{""tag_name"":""v2.4.1""}"));
            var sut = new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromSeconds(5));

            var json = sut.GetLatestReleaseJson();

            Assert.Equal(@"{""tag_name"":""v2.4.1""}", json);
            Assert.Equal("v2.4.1", LatestRelease.Parse(json).TagName);
        }

        [Fact]
        public void identifies_itself_and_asks_for_json()
        {
            var url = StartServer(stream => Send(stream, "200 OK", "{}"));

            new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromSeconds(5)).GetLatestReleaseJson();

            Assert.True(receivedRequest.StartsWith("GET /repos/doggy8088/carnac/releases/latest HTTP/1.1", StringComparison.Ordinal), receivedRequest);
            Assert.Contains("User-Agent: Carnac/2.3.0", receivedRequest);
            Assert.Contains("Accept: application/vnd.github+json", receivedRequest);
        }

        [Fact]
        public void reads_utf8_text()
        {
            var body = "{\"name\":\"caf" + (char)0xE9 + "\"}";
            var url = StartServer(stream => Send(stream, "200 OK", body));

            Assert.Equal(body, new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromSeconds(5)).GetLatestReleaseJson());
        }

        [Fact]
        public void an_http_error_status_is_thrown_as_a_web_exception()
        {
            var url = StartServer(stream => Send(stream, "404 Not Found", @"{""message"":""Not Found""}"));

            var exception = Assert.Throws<WebException>(() => new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromSeconds(5)).GetLatestReleaseJson());

            Assert.Equal(WebExceptionStatus.ProtocolError, exception.Status);
            Assert.Equal(HttpStatusCode.NotFound, ((HttpWebResponse)exception.Response).StatusCode);
        }

        [Fact]
        public void the_rate_limit_answer_is_thrown_as_a_web_exception()
        {
            var url = StartServer(stream => Send(stream, "403 Forbidden", @"{""message"":""API rate limit exceeded""}"));

            Assert.Throws<WebException>(() => new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromSeconds(5)).GetLatestReleaseJson());
        }

        [Fact]
        public void gives_up_when_the_server_does_not_answer()
        {
            var url = StartServer(stream => stop.WaitOne(10000));

            var exception = Assert.Throws<WebException>(() => new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromMilliseconds(500)).GetLatestReleaseJson());

            Assert.Equal(WebExceptionStatus.Timeout, exception.Status);
        }

        [Fact]
        public void refuses_an_answer_that_is_too_large()
        {
            var url = StartServer(stream => Send(stream, "200 OK", new string('x', GitHubReleaseFeed.MaxResponseBytes + 1000)));

            Assert.Throws<InvalidDataException>(() => new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.FromSeconds(10)).GetLatestReleaseJson());
        }

        [Fact]
        public void the_real_address_is_the_github_api_of_this_repository()
        {
            Assert.Equal("https://api.github.com/repos/doggy8088/carnac/releases/latest", GitHubReleaseFeed.LatestReleaseUrl);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            var url = new Uri("http://127.0.0.1:1/");
            Assert.Throws<ArgumentNullException>(() => new GitHubReleaseFeed(null, "Carnac/2.3.0", TimeSpan.FromSeconds(5)));
            Assert.Throws<ArgumentException>(() => new GitHubReleaseFeed(url, "", TimeSpan.FromSeconds(5)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GitHubReleaseFeed(url, "Carnac/2.3.0", TimeSpan.Zero));
        }
    }
}
