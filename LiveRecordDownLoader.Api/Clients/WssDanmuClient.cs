using Api.Model.DanmuConf;
using Microsoft.Extensions.Logging;
using Nerdbank.Streams;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Clients
{
	public class WssDanmuClient : DanmuClientBase
	{
		//ws://和wss://前缀分别表示WebSocket连接和安全的WebSocket连接。
		protected override string Server => $@"wss://{Host}:{Port}/sub";
		protected override ushort DefaultPort => 443;
		protected override bool ClientConnected => _client?.State == WebSocketState.Open;

		private ClientWebSocket? _client;

		public WssDanmuClient(ILogger<WssDanmuClient> logger, BilibiliApiClient apiClient) : base(logger, apiClient) { }

		protected override ushort GetPort(HostServerList server)
		{
			return server.wss_port;
		}

		protected override IDisposable CreateClient()
		{
			_client = new();
			_client.Options.Proxy = WebRequest.DefaultWebProxy;
			return _client;
		}

		protected override async ValueTask<IDuplexPipe> ClientHandshakeAsync(CancellationToken token)
		{
			await _client!.ConnectAsync(new(Server), token);
			return _client.UsePipe(BufferSize, cancellationToken: token);
		}
	}
}
