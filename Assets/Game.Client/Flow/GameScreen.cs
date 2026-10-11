namespace Game.Client.Flow
{
    // Top-level screens. Exactly one is shown at a time; GameFlowController owns the switching.
    public enum GameScreen
    {
        Title,
        Home,
        // 무공 제작: the only place techniques are drawn and saved to slots.
        Workshop,
        // 수련: PvE stage select.
        Training,
        // 비무: PvP lobby.
        Sparring,
        // Battle replay, entered from Training or Sparring.
        Battle,
    }
}
