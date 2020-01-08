namespace WalletWasabi.Gui.Rpc
{
    public enum JsonRpcErrorCodes
    {
        ParseError = -32700,  // Invalid JSON was received by the server. An error occurred on the server while parsing the JSON text.
        InvalidRequest = -32600,  // The JSON sent is not a valid Request object.
        MethodNotFound = -32601,  // The method does not exist / is not available.
        InvalidParams = -32602,  // Invalid method parameter(s).
        InternalError = -32603,  // Internal JSON-RPC error.
    }
}
