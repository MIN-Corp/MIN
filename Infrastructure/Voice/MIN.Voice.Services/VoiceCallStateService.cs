using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.Voice.Services.Contacts.Interfaces;

namespace MIN.Voice.Services;

/// <inheritdoc cref="IVoiceCallStateService"/>
public class VoiceCallStateService : IVoiceCallStateService
{
    // There could be only one active call at a time
    private SubRoomContext? activeVoiceCall;

    void IVoiceCallStateService.RegisterVoiceCall(Guid roomId, int subRoomId)
        => activeVoiceCall = new(roomId, subRoomId);

    SubRoomContext? IVoiceCallStateService.GetRoomVoiceCallContext()
        => activeVoiceCall;

    void IVoiceCallStateService.UnregisterVoiceCall()
        => activeVoiceCall = null;

    bool IVoiceCallStateService.IsInVoiceCall(Guid? roomId, int? subRoomId)
    {
        if (roomId != null && subRoomId != null)
        {
            return activeVoiceCall != null
                && activeVoiceCall.Value.RoomId == roomId
                && activeVoiceCall.Value.SubRoomId == subRoomId;
        }

        if (roomId != null)
        {
            return activeVoiceCall != null && activeVoiceCall.Value.RoomId == roomId;
        }

        if (subRoomId != null)
        {
            return activeVoiceCall != null && activeVoiceCall.Value.SubRoomId == subRoomId;
        }

        return activeVoiceCall != null;
    }
}
