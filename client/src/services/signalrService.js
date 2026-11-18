import * as signalR from '@microsoft/signalr';

export function createGameHubConnection(token, roomCode = '') {
  const url = roomCode ? `/hubs/game?roomCode=${encodeURIComponent(roomCode)}` : '/hubs/game';

  return new signalR.HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => token
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}
