namespace UmbrellaPuzzle.Environmental
{
    // 환경 퍼즐 구역의 시각 컴포넌트가 공통으로 제공할 최소 제어 API다.
    public interface IEnvironmentZoneVisual
    {
        EnvironmentZoneState CurrentState { get; }
        float CurrentIntensity { get; }
        float TargetIntensity { get; }

        void SetState(EnvironmentZoneState nextState);
        void SetActive();
        void SetInactive();
        void SetSolved();
        void RefreshGeometry();
    }
}
