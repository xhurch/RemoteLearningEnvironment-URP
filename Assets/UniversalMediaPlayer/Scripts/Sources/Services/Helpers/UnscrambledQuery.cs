namespace UMP.Services.Helpers
{
    internal struct UnscrambledQuery
    {
        private string _uri;
        private bool _encrypted;
        private string _sp;

        public UnscrambledQuery(string uri, bool encrypted, string sp)
        {
            _uri = uri;
            _encrypted = encrypted;
            _sp = sp;
        }

        public string Uri
        {
            get { return _uri; }
        }

        public bool IsEncrypted
        {
            get { return _encrypted; }
        }

        public string Sp
        {
            get { return _sp; }
        }
    }
}
