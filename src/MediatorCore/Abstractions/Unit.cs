namespace MediatorCore;

public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    public static readonly Unit Value = new();

    public int CompareTo(Unit other) => 0;
    public int CompareTo(object? obj) => 0;
    public override bool Equals(object? obj) => obj is Unit;
    public bool Equals(Unit other) => true;
    public override int GetHashCode() => 0;
    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
    public override string ToString() => "()";
}