using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serverless.CLI.Responses;

public class FunctionList
{
    public List<FunctionInfo> Items { get; set; }
}

public class FunctionInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Route { get; set; }
    public int Method { get; set; }
    public string? CronExpression { get; set; }
    public DateTime? LastExecution { get; set; }
    public DateTime? NextExecution { get; set; }
    public Guid AssemblyId { get; set; }
}

