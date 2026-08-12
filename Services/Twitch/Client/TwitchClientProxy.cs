using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;

namespace MARS.Server.Services.Twitch.Client;

/// <summary>
/// Proxy implementing ITwitchClient that delegates to an inner client.
/// Supports atomic client swap for transparent recreation (Adapter + Observer + Bridge).
/// The 5 used events (OnMessageReceived, OnConnected, OnDisconnected, OnConnectionError, OnMessageCleared)
/// are forwarded through the proxy so they survive client swap.
/// </summary>
public class TwitchClientProxy : ITwitchClient
{
    private volatile ITwitchClient _inner;
    private readonly Lock _swapLock = new();

    public TwitchClientProxy(ITwitchClient inner)
    {
        _inner = inner;
        WireUsedEvents();
    }

    /// <summary>
    /// Atomically swaps the inner client. Used event forwarding is re-wired.
    /// Old client is disposed after swap.
    /// </summary>
    internal void ReplaceClient(ITwitchClient newClient)
    {
        ITwitchClient? oldClient;
        lock (_swapLock)
        {
            oldClient = _inner;

            // Unsubscribe from old
            oldClient.OnMessageReceived -= ForwardOnMessageReceived;
            oldClient.OnConnected -= ForwardOnConnected;
            oldClient.OnDisconnected -= ForwardOnDisconnected;
            oldClient.OnConnectionError -= ForwardOnConnectionError;
            oldClient.OnMessageCleared -= ForwardOnMessageCleared;

            _inner = newClient;

            // Re-subscribe to new
            newClient.OnMessageReceived += ForwardOnMessageReceived;
            newClient.OnConnected += ForwardOnConnected;
            newClient.OnDisconnected += ForwardOnDisconnected;
            newClient.OnConnectionError += ForwardOnConnectionError;
            newClient.OnMessageCleared += ForwardOnMessageCleared;
        }

        (oldClient as IDisposable)?.Dispose();
    }

    private void WireUsedEvents()
    {
        _inner.OnMessageReceived += ForwardOnMessageReceived;
        _inner.OnConnected += ForwardOnConnected;
        _inner.OnDisconnected += ForwardOnDisconnected;
        _inner.OnConnectionError += ForwardOnConnectionError;
        _inner.OnMessageCleared += ForwardOnMessageCleared;
    }

    #region Used event forwarding (5 events)

    public event AsyncEventHandler<OnMessageReceivedArgs>? OnMessageReceived;
    public event AsyncEventHandler<TwitchLib.Client.Events.OnConnectedEventArgs>? OnConnected;
    public event AsyncEventHandler<OnDisconnectedArgs>? OnDisconnected;
    public event AsyncEventHandler<OnConnectionErrorArgs>? OnConnectionError;
    public event AsyncEventHandler<OnMessageClearedArgs>? OnMessageCleared;

    private Task ForwardOnMessageReceived(object? sender, OnMessageReceivedArgs args) =>
        OnMessageReceived?.Invoke(sender, args) ?? Task.CompletedTask;

    private Task ForwardOnConnected(
        object? sender,
        TwitchLib.Client.Events.OnConnectedEventArgs args
    ) => OnConnected?.Invoke(sender, args) ?? Task.CompletedTask;

    private Task ForwardOnDisconnected(object? sender, OnDisconnectedArgs args) =>
        OnDisconnected?.Invoke(sender, args) ?? Task.CompletedTask;

    private Task ForwardOnConnectionError(object? sender, OnConnectionErrorArgs args) =>
        OnConnectionError?.Invoke(sender, args) ?? Task.CompletedTask;

    private Task ForwardOnMessageCleared(object? sender, OnMessageClearedArgs args) =>
        OnMessageCleared?.Invoke(sender, args) ?? Task.CompletedTask;

    #endregion

    #region Unused events — explicit interface impl, delegates directly to inner (no swap)

#pragma warning disable CS0067
#pragma warning disable CS8615 // Nullability of reference types in return type doesn't match overridden member

    event AsyncEventHandler<OnAnnouncementArgs> ITwitchClient.OnAnnouncement
    {
        add => _inner.OnAnnouncement += value;
        remove => _inner.OnAnnouncement -= value;
    }

    event AsyncEventHandler<OnJoinedChannelArgs> ITwitchClient.OnJoinedChannel
    {
        add => _inner.OnJoinedChannel += value;
        remove => _inner.OnJoinedChannel -= value;
    }

    event AsyncEventHandler<OnIncorrectLoginArgs> ITwitchClient.OnIncorrectLogin
    {
        add => _inner.OnIncorrectLogin += value;
        remove => _inner.OnIncorrectLogin -= value;
    }

    event AsyncEventHandler<OnChannelStateChangedArgs> ITwitchClient.OnChannelStateChanged
    {
        add => _inner.OnChannelStateChanged += value;
        remove => _inner.OnChannelStateChanged -= value;
    }

    event AsyncEventHandler<OnUserStateChangedArgs> ITwitchClient.OnUserStateChanged
    {
        add => _inner.OnUserStateChanged += value;
        remove => _inner.OnUserStateChanged -= value;
    }

    event AsyncEventHandler<OnWhisperReceivedArgs> ITwitchClient.OnWhisperReceived
    {
        add => _inner.OnWhisperReceived += value;
        remove => _inner.OnWhisperReceived -= value;
    }

    event AsyncEventHandler<OnMessageSentArgs> ITwitchClient.OnMessageSent
    {
        add => _inner.OnMessageSent += value;
        remove => _inner.OnMessageSent -= value;
    }

    event AsyncEventHandler<OnChatCommandReceivedArgs> ITwitchClient.OnChatCommandReceived
    {
        add => _inner.OnChatCommandReceived += value;
        remove => _inner.OnChatCommandReceived -= value;
    }

    event AsyncEventHandler<OnWhisperCommandReceivedArgs> ITwitchClient.OnWhisperCommandReceived
    {
        add => _inner.OnWhisperCommandReceived += value;
        remove => _inner.OnWhisperCommandReceived -= value;
    }

    event AsyncEventHandler<OnUserJoinedArgs> ITwitchClient.OnUserJoined
    {
        add => _inner.OnUserJoined += value;
        remove => _inner.OnUserJoined -= value;
    }

    event AsyncEventHandler<OnNewSubscriberArgs> ITwitchClient.OnNewSubscriber
    {
        add => _inner.OnNewSubscriber += value;
        remove => _inner.OnNewSubscriber -= value;
    }

    event AsyncEventHandler<OnReSubscriberArgs> ITwitchClient.OnReSubscriber
    {
        add => _inner.OnReSubscriber += value;
        remove => _inner.OnReSubscriber -= value;
    }

    event AsyncEventHandler<OnPrimePaidSubscriberArgs> ITwitchClient.OnPrimePaidSubscriber
    {
        add => _inner.OnPrimePaidSubscriber += value;
        remove => _inner.OnPrimePaidSubscriber -= value;
    }

    event AsyncEventHandler<OnExistingUsersDetectedArgs> ITwitchClient.OnExistingUsersDetected
    {
        add => _inner.OnExistingUsersDetected += value;
        remove => _inner.OnExistingUsersDetected -= value;
    }

    event AsyncEventHandler<OnUserLeftArgs> ITwitchClient.OnUserLeft
    {
        add => _inner.OnUserLeft += value;
        remove => _inner.OnUserLeft -= value;
    }

    event AsyncEventHandler<OnChatClearedArgs> ITwitchClient.OnChatCleared
    {
        add => _inner.OnChatCleared += value;
        remove => _inner.OnChatCleared -= value;
    }

    event AsyncEventHandler<OnUserTimedoutArgs> ITwitchClient.OnUserTimedout
    {
        add => _inner.OnUserTimedout += value;
        remove => _inner.OnUserTimedout -= value;
    }

    event AsyncEventHandler<OnLeftChannelArgs> ITwitchClient.OnLeftChannel
    {
        add => _inner.OnLeftChannel += value;
        remove => _inner.OnLeftChannel -= value;
    }

    event AsyncEventHandler<OnUserBannedArgs> ITwitchClient.OnUserBanned
    {
        add => _inner.OnUserBanned += value;
        remove => _inner.OnUserBanned -= value;
    }

    event AsyncEventHandler<OnSendReceiveDataArgs> ITwitchClient.OnSendReceiveData
    {
        add => _inner.OnSendReceiveData += value;
        remove => _inner.OnSendReceiveData -= value;
    }

    event AsyncEventHandler<OnRaidNotificationArgs> ITwitchClient.OnRaidNotification
    {
        add => _inner.OnRaidNotification += value;
        remove => _inner.OnRaidNotification -= value;
    }

    event AsyncEventHandler<OnGiftedSubscriptionArgs> ITwitchClient.OnGiftedSubscription
    {
        add => _inner.OnGiftedSubscription += value;
        remove => _inner.OnGiftedSubscription -= value;
    }

    event AsyncEventHandler<OnCommunitySubscriptionArgs> ITwitchClient.OnCommunitySubscription
    {
        add => _inner.OnCommunitySubscription += value;
        remove => _inner.OnCommunitySubscription -= value;
    }

    event AsyncEventHandler<OnContinuedGiftedSubscriptionArgs> ITwitchClient.OnContinuedGiftedSubscription
    {
        add => _inner.OnContinuedGiftedSubscription += value;
        remove => _inner.OnContinuedGiftedSubscription -= value;
    }

    event AsyncEventHandler<OnAnonGiftPaidUpgradeArgs> ITwitchClient.OnAnonGiftPaidUpgrade
    {
        add => _inner.OnAnonGiftPaidUpgrade += value;
        remove => _inner.OnAnonGiftPaidUpgrade -= value;
    }

    event AsyncEventHandler<OnUnraidNotificationArgs> ITwitchClient.OnUnraidNotification
    {
        add => _inner.OnUnraidNotification += value;
        remove => _inner.OnUnraidNotification -= value;
    }

    event AsyncEventHandler<OnRitualArgs> ITwitchClient.OnRitual
    {
        add => _inner.OnRitual += value;
        remove => _inner.OnRitual -= value;
    }

    event AsyncEventHandler<OnBitsBadgeTierArgs> ITwitchClient.OnBitsBadgeTier
    {
        add => _inner.OnBitsBadgeTier += value;
        remove => _inner.OnBitsBadgeTier -= value;
    }

    event AsyncEventHandler<OnCommunityPayForwardArgs> ITwitchClient.OnCommunityPayForward
    {
        add => _inner.OnCommunityPayForward += value;
        remove => _inner.OnCommunityPayForward -= value;
    }

    event AsyncEventHandler<OnStandardPayForwardArgs> ITwitchClient.OnStandardPayForward
    {
        add => _inner.OnStandardPayForward += value;
        remove => _inner.OnStandardPayForward -= value;
    }

    event AsyncEventHandler<OnMessageThrottledArgs> ITwitchClient.OnMessageThrottled
    {
        add => _inner.OnMessageThrottled += value;
        remove => _inner.OnMessageThrottled -= value;
    }

    event AsyncEventHandler<OnErrorEventArgs> ITwitchClient.OnError
    {
        add => _inner.OnError += value;
        remove => _inner.OnError -= value;
    }

    event AsyncEventHandler<TwitchLib.Client.Events.OnConnectedEventArgs> ITwitchClient.OnReconnected
    {
        add => _inner.OnReconnected += value;
        remove => _inner.OnReconnected -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnRequiresVerifiedEmail
    {
        add => _inner.OnRequiresVerifiedEmail += value;
        remove => _inner.OnRequiresVerifiedEmail -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnRequiresVerifiedPhoneNumber
    {
        add => _inner.OnRequiresVerifiedPhoneNumber += value;
        remove => _inner.OnRequiresVerifiedPhoneNumber -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnRateLimit
    {
        add => _inner.OnRateLimit += value;
        remove => _inner.OnRateLimit -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnDuplicate
    {
        add => _inner.OnDuplicate += value;
        remove => _inner.OnDuplicate -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnBannedEmailAlias
    {
        add => _inner.OnBannedEmailAlias += value;
        remove => _inner.OnBannedEmailAlias -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnSelfRaidError
    {
        add => _inner.OnSelfRaidError += value;
        remove => _inner.OnSelfRaidError -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnNoPermissionError
    {
        add => _inner.OnNoPermissionError += value;
        remove => _inner.OnNoPermissionError -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnRaidedChannelIsMatureAudience
    {
        add => _inner.OnRaidedChannelIsMatureAudience += value;
        remove => _inner.OnRaidedChannelIsMatureAudience -= value;
    }

    event AsyncEventHandler<OnFailureToReceiveJoinConfirmationArgs> ITwitchClient.OnFailureToReceiveJoinConfirmation
    {
        add => _inner.OnFailureToReceiveJoinConfirmation += value;
        remove => _inner.OnFailureToReceiveJoinConfirmation -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnFollowersOnly
    {
        add => _inner.OnFollowersOnly += value;
        remove => _inner.OnFollowersOnly -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnSubsOnly
    {
        add => _inner.OnSubsOnly += value;
        remove => _inner.OnSubsOnly -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnEmoteOnly
    {
        add => _inner.OnEmoteOnly += value;
        remove => _inner.OnEmoteOnly -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnSuspended
    {
        add => _inner.OnSuspended += value;
        remove => _inner.OnSuspended -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnBanned
    {
        add => _inner.OnBanned += value;
        remove => _inner.OnBanned -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnSlowMode
    {
        add => _inner.OnSlowMode += value;
        remove => _inner.OnSlowMode -= value;
    }

    event AsyncEventHandler<NoticeEventArgs> ITwitchClient.OnR9kMode
    {
        add => _inner.OnR9kMode += value;
        remove => _inner.OnR9kMode -= value;
    }

    event AsyncEventHandler<OnUserIntroArgs> ITwitchClient.OnUserIntro
    {
        add => _inner.OnUserIntro += value;
        remove => _inner.OnUserIntro -= value;
    }

    event AsyncEventHandler<OnUnaccountedForArgs> ITwitchClient.OnUnaccountedFor
    {
        add => _inner.OnUnaccountedFor += value;
        remove => _inner.OnUnaccountedFor -= value;
    }

#pragma warning restore CS0067
#pragma warning restore CS8615

    #endregion

    #region Properties

    public MessageEmoteCollection ChannelEmotes => _inner.ChannelEmotes;

    public ConnectionCredentials? ConnectionCredentials => _inner.ConnectionCredentials;

    public bool DisableAutoPong
    {
        get => _inner.DisableAutoPong;
        set => _inner.DisableAutoPong = value;
    }

    public bool IsConnected => _inner.IsConnected;

    public bool IsInitialized => _inner.IsInitialized;

    public IReadOnlyList<JoinedChannel> JoinedChannels => _inner.JoinedChannels;

    public WhisperMessage? PreviousWhisper => _inner.PreviousWhisper;

    public string TwitchUsername => _inner.TwitchUsername;

    public bool WillReplaceEmotes
    {
        get => _inner.WillReplaceEmotes;
        set => _inner.WillReplaceEmotes = value;
    }

    public ICollection<string> ChatCommandIdentifiers => _inner.ChatCommandIdentifiers;

    public ICollection<string> WhisperCommandIdentifiers => _inner.WhisperCommandIdentifiers;

    #endregion

    #region Methods

    public void Initialize(ConnectionCredentials credentials, string? channel = null) =>
        _inner.Initialize(credentials, channel);

    public void Initialize(ConnectionCredentials credentials, List<string> channels) =>
        _inner.Initialize(credentials, channels);

    public void SetConnectionCredentials(ConnectionCredentials credentials) =>
        _inner.SetConnectionCredentials(credentials);

    public Task<bool> ConnectAsync() => _inner.ConnectAsync();

    public Task DisconnectAsync() => _inner.DisconnectAsync();

    public Task ReconnectAsync() => _inner.ReconnectAsync();

    public JoinedChannel? GetJoinedChannel(string channel) => _inner.GetJoinedChannel(channel);

    public Task JoinChannelAsync(string channel, bool overrideCheck = false) =>
        _inner.JoinChannelAsync(channel, overrideCheck);

    public Task LeaveChannelAsync(JoinedChannel channel) => _inner.LeaveChannelAsync(channel);

    public Task LeaveChannelAsync(string channel) => _inner.LeaveChannelAsync(channel);

    public Task OnReadLineTestAsync(string rawIRC) => _inner.OnReadLineTestAsync(rawIRC);

    public Task SendMessageAsync(JoinedChannel channel, string message, bool dryRun = false) =>
        _inner.SendMessageAsync(channel, message, dryRun);

    public Task SendMessageAsync(string channel, string message, bool dryRun = false) =>
        _inner.SendMessageAsync(channel, message, dryRun);

    public Task SendReplyAsync(
        JoinedChannel channel,
        string replyToId,
        string message,
        bool dryRun = false
    ) => _inner.SendReplyAsync(channel, replyToId, message, dryRun);

    public Task SendReplyAsync(
        string channel,
        string replyToId,
        string message,
        bool dryRun = false
    ) => _inner.SendReplyAsync(channel, replyToId, message, dryRun);

    public Task SendQueuedItemAsync(string message) => _inner.SendQueuedItemAsync(message);

    public Task SendRawAsync(string message) => _inner.SendRawAsync(message);

    #endregion
}
