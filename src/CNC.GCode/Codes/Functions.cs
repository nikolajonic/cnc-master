namespace CNC.GCode.Codes;

/// <summary>
/// Supported G-codes. The numeric value is the code number times ten, so decimal codes such as
/// G43.1 (431) can be added without renumbering.
/// </summary>
public enum GFunction
{
    G0 = 0,
    G1 = 10,
    G2 = 20,
    G3 = 30,
    G17 = 170,
    G18 = 180,
    G19 = 190,
    G20 = 200,
    G21 = 210,
    G28 = 280,
    G40 = 400,
    G41 = 410,
    G42 = 420,
    G43 = 430,
    G49 = 490,
    G54 = 540,
    G55 = 550,
    G56 = 560,
    G57 = 570,
    G58 = 580,
    G59 = 590,
    G80 = 800,
    G90 = 900,
    G91 = 910,
    G94 = 940,
}

/// <summary>Supported M-codes.</summary>
public enum MFunction
{
    M0 = 0,
    M1 = 1,
    M2 = 2,
    M3 = 3,
    M4 = 4,
    M5 = 5,
    M6 = 6,
    M30 = 30,
}

/// <summary>Modal groups as defined by RS274/NGC. At most one code per group may appear in a block.</summary>
public enum ModalGroup
{
    NonModal,
    Motion,
    Plane,
    DistanceMode,
    FeedRateMode,
    Units,
    CutterCompensation,
    ToolLengthOffset,
    CoordinateSystem,
    Stopping,
    ToolChange,
    Spindle,
}
