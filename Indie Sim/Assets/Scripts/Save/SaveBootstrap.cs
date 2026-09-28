/// <summary>
/// Save composition root: builds the SaveService and registers every section,
/// once, at boot. Adding saved data later = one new SaveSection class + one
/// Register line here; SaveService itself never changes.
/// </summary>
public static class SaveBootstrap
{
    public static SaveService Create(GameSession session)
    {
        SaveService saves = new SaveService(SaveService.DefaultFolder);

        // Sections arrive in 4B (slot.meta, run.*, global.*).

        return saves;
    }
}
