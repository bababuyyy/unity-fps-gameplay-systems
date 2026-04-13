/// <summary>
/// Define os tipos de dano aplicáveis no jogo para cálculo de resistências e fraquezas.
/// </summary>
public enum DamageType
{
    Slashing,
    Blunt,
    Pierce,
    Magic,
    Fire
}

/// <summary>
/// Define as partes do corpo segmentadas para o Trauma System.
/// </summary>
public enum BodyPart
{
    Head,
    Torso,
    LeftArm,
    LeftForearm,
    RightArm,
    RightForearm,
    LeftLeg,
    LeftAnkle,
    RightLeg,
    RightAnkle
}