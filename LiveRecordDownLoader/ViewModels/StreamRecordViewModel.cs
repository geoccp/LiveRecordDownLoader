using LiveRecordDownLoader.Enums;
using LiveRecordDownLoader.Models;
using LiveRecordDownLoader.Views.Dialogs;
using DynamicData;
using Microsoft.Extensions.Logging;
using ModernWpf.Controls;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRecordDownLoader.ViewModels
{
	public class StreamRecordViewModel : ReactiveObject, IRoutableViewModel
	{
		public string UrlPathSegment => @"StreamRecord";
		public IScreen HostScreen { get; }

		#region Command

		public ReactiveCommand<Unit, Unit> AddRoomCommand { get; }
		public ReactiveCommand<object?, Unit> ModifyRoomCommand { get; }
		public ReactiveCommand<object?, Unit> RemoveRoomCommand { get; }
		public ReactiveCommand<object?, Unit> RefreshRoomCommand { get; }
		public ReactiveCommand<object?, Unit> OpenDirCommand { get; }
		public ReactiveCommand<object?, Unit> OpenUrlCommand { get; }

		#endregion

		private readonly ILogger _logger;
		private readonly SourceList<RoomStatus> _roomList;
		private readonly Config _config;

		public readonly ReadOnlyObservableCollection<RoomStatus> RoomList;

		public StreamRecordViewModel(
			IScreen hostScreen,
			ILogger<StreamRecordViewModel> logger,
			SourceList<RoomStatus> roomList,
			Config config)
		{
			HostScreen = hostScreen;
			_logger = logger;
			_roomList = roomList;
			_config = config;

			_roomList.Connect()
					.ObserveOnDispatcher()
					.Bind(out RoomList)
					.DisposeMany()
					.Subscribe(RoomListChanged);

			AddRoomCommand = ReactiveCommand.CreateFromTask(AddRoomAsync);
			ModifyRoomCommand = ReactiveCommand.CreateFromTask<object?, Unit>(ModifyRoomAsync);
			RemoveRoomCommand = ReactiveCommand.CreateFromTask<object?, Unit>(RemoveRoomAsync);
			RefreshRoomCommand = ReactiveCommand.CreateFromTask<object?, Unit>(RefreshRoomAsync);
			OpenDirCommand = ReactiveCommand.CreateFromObservable<object?, Unit>(OpenDir);
			OpenUrlCommand = ReactiveCommand.CreateFromObservable<object?, Unit>(OpenLiveUrl);
		}

		private static void RoomListChanged(IChangeSet<RoomStatus> changeSet)
		{
			foreach (var change in changeSet)
			{
				switch (change.Reason)
				{
					case ListChangeReason.Add:
					case ListChangeReason.AddRange:
					{
						switch (change.Type)
						{
							case ChangeType.Item:
							{
								var room = change.Item.Current;
								room.Start();
								break;
							}
							case ChangeType.Range:
							{
								foreach (var room in change.Range)
								{
									room.Start();
								}

								break;
							}
						}

						break;
					}
					case ListChangeReason.Remove:
					case ListChangeReason.RemoveRange:
					case ListChangeReason.Clear:
					{
						switch (change.Type)
						{
							case ChangeType.Item:
							{
								var room = change.Item.Current;
								room.Stop();
								break;
							}
							case ChangeType.Range:
							{
								foreach (var room in change.Range)
								{
									room.Stop();
								}

								break;
							}
						}

						break;
					}
				}
			}
		}

		private void RaiseRoomsChanged()
		{
			_config.RaisePropertyChanged(nameof(_config.Rooms));
		}

		private async Task AddRoomAsync(CancellationToken token)
		{
			try
			{
				var room = new RoomStatus();
				using (var dialog = new RoomDialog(RoomDialogType.Add, room))
				{
					if (await dialog.SafeShowAsync() != ContentDialogResult.Primary)
					{
						return;
					}
				}

				await room.GetRoomInfoDataAsync(true, token);

				if (_roomList.Items.Any(x => x.RoomId == room.RoomId))
				{
					using var dialog = new DisposableContentDialog
					{
						Title = @"???????????????",
						Content = @"????????????????????????",
						PrimaryButtonText = @"??????",
						DefaultButton = ContentDialogButton.Primary
					};
					await dialog.SafeShowAsync();
					return;
				}

				AddRoom(room);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, @"??????????????????");
				var message = ex is JsonException ? @"????????????????????????" : ex.Message;
				using var dialog = new DisposableContentDialog
				{
					Title = @"??????????????????",
					Content = message,
					PrimaryButtonText = @"??????",
					DefaultButton = ContentDialogButton.Primary
				};
				await dialog.SafeShowAsync();
			}
		}

		private void AddRoom(RoomStatus room)
		{
			_roomList.Add(room);
			_config.Rooms.Add(room);
			RaiseRoomsChanged();
		}

		private async Task<Unit> ModifyRoomAsync(object? data, CancellationToken token)
		{
			try
			{
				if (data is not RoomStatus room)
				{
					return default;
				}
				var roomCopy = room.Clone();
				using (var dialog = new RoomDialog(RoomDialogType.Modify, roomCopy))
				{
					if (await dialog.SafeShowAsync() != ContentDialogResult.Primary)
					{
						return default;
					}
				}
				await room.UpdateAsync(roomCopy);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, @"??????????????????");
				using var dialog = new DisposableContentDialog
				{
					Title = @"??????????????????",
					Content = ex.Message,
					PrimaryButtonText = @"??????",
					DefaultButton = ContentDialogButton.Primary
				};
				await dialog.SafeShowAsync();
			}
			return default;
		}

		private async Task<Unit> RemoveRoomAsync(object? data, CancellationToken token)
		{
			try
			{
				if (data is not IList { Count: > 0 } list)
				{
					return default;
				}
				var rooms = new List<RoomStatus>();
				foreach (var item in list)
				{
					if (item is not RoomStatus room)
					{
						continue;
					}
					rooms.Add(room);
				}
				var roomList = string.Join('???', rooms.Select(room => string.IsNullOrWhiteSpace(room.UserName) ? $@"{room.RoomId}" : room.UserName));
				using (var dialog = new DisposableContentDialog
				{
					Title = @"????????????????????????",
					Content = roomList,
					PrimaryButtonText = @"??????",
					CloseButtonText = @"??????",
					DefaultButton = ContentDialogButton.Close
				})
				{
					if (await dialog.SafeShowAsync() != ContentDialogResult.Primary)
					{
						return default;
					}
				}
				RemoveRoom(rooms);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, @"??????????????????");
				using var dialog = new DisposableContentDialog
				{
					Title = @"??????????????????",
					Content = ex.Message,
					PrimaryButtonText = @"??????",
					DefaultButton = ContentDialogButton.Primary
				};
				await dialog.SafeShowAsync();
			}
			return default;
		}

		private void RemoveRoom(List<RoomStatus> rooms)
		{
			_roomList.RemoveMany(rooms);
			_config.Rooms.RemoveMany(rooms);
			RaiseRoomsChanged();
		}

		private async Task<Unit> RefreshRoomAsync(object? data, CancellationToken token)
		{
			try
			{
				if (data is not IList { Count: > 0 } list)
				{
					return default;
				}

				foreach (var item in list)
				{
					if (item is not RoomStatus room)
					{
						continue;
					}
					await room.RefreshStatusAsync(token);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, @"????????????????????????");
			}
			return default;
		}

		private IObservable<Unit> OpenDir(object? data)
		{
			return Observable.Start(() =>
			{
				try
				{
					if (data is RoomStatus room)
					{
						var path = Path.Combine(_config.MainDir, $@"{room.RoomId}");
						if (!Directory.Exists(path))
						{
							Directory.CreateDirectory(path);
						}
						Utils.Utils.OpenDir(path);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, @"??????????????????");
				}
			});
		}

		private IObservable<Unit> OpenLiveUrl(object? data)
		{
			return Observable.Start(() =>
			{
				try
				{
					if (data is not IList { Count: > 0 } list)
					{
						return;
					}
					foreach (var item in list)
					{
						if (item is not RoomStatus room)
						{
							continue;
						}
						Utils.Utils.OpenUrl($@"https://live.bilibili.com/{room.RoomId}");
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, @"?????????????????????");
				}
			});
		}
	}
}
