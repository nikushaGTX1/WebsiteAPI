namespace Website_API.Models
{
    public class StreetModels
    {
        public int Id { get; set; }
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public List<string> StreetNames { get; set; } = new();
    }
}
