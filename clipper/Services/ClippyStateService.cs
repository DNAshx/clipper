namespace clipper.Services
{
    public enum ClippyState
    {
        Idle,
        Speaking,
        Sleeping,
        WakingUp
    }

    public class ClippyStateService
    {
        public ClippyState CurrentState { get; private set; }
            = ClippyState.Idle;

        public bool IsSleeping =>
            CurrentState == ClippyState.Sleeping;

        public bool CanBlink =>
            CurrentState == ClippyState.Idle ||
            CurrentState == ClippyState.Speaking;

        public bool CanReact =>
            CurrentState == ClippyState.Idle;

        public void SetIdle()
        {
            CurrentState = ClippyState.Idle;
        }

        public void SetSpeaking()
        {
            CurrentState = ClippyState.Speaking;
        }

        public void SetSleeping()
        {
            CurrentState = ClippyState.Sleeping;
        }

        public void SetWakingUp()
        {
            CurrentState = ClippyState.WakingUp;
        }
    }
}