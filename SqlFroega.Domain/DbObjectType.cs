namespace SqlFroega.Domain;

public enum DbObjectType
{
    Table = 0,
    View = 1,
    Procedure = 2,
    Function = 3,
    Schema = 4,
    Database = 5,
    Unknown = 98,
    Other = 99
}
