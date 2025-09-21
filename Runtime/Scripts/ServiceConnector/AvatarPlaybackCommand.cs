namespace IVH.Core.ServiceConnector
{
    public struct AvatarPlaybackCommand
    {
        public string audioId;
        public string actionFunction;
        public string emotionFunction;

        public AvatarPlaybackCommand(string audioId, string actionFunction, string emotionFunction)
        {
            this.audioId = audioId;
            this.actionFunction = actionFunction;
            this.emotionFunction = emotionFunction;
        }
    }
}