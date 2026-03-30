using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DXApplication1.Data
{
    public class JsonDataConnectionDescription : DataConnection { 
    }
    public abstract class DataConnection
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string ConnectionString { get; set; }
    }
}