// What a level's story tells the TimeMachine: whether the parts are collected, and what happens on repair and boarding.
// MedievalLevel (Time Crystal) and CavemanLevel (magnetic stones) implement it. The TimeMachine finds it in the scene.
public interface ITimeMachineStory
{
    bool CanRepair { get; }

    // Lines when the player inspects the machine without the parts. Null uses the machine's own Broken Lines
    string[] NotReadyLines { get; }

    void TimeMachineRepaired();

    // travelling = the next era is about to load
    void TimeMachineBoarded(bool travelling);
}
