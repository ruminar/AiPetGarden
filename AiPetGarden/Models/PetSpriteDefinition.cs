namespace AiPetGarden.Models;

public sealed record PetSpriteDefinition(
    string SpriteSheetPath,
    int Columns,
    int Rows,
    int CellWidth,
    int CellHeight,
    int NeutralRowIndex,
    int NeutralColumnIndex);
