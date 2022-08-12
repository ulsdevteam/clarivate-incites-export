namespace clarivate_incites_export;

[System.Serializable]
class ConnectionException : System.Exception
{
    public ConnectionException(string message, System.Exception inner) : base(message, inner) { }

    protected ConnectionException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

    public override string ToString()
    {
        return Message;
    }
}