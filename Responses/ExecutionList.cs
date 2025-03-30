using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serverless.CLI.Responses;

public class ExecutionList
{
    public List<ExecutionInfo> Items { get; set; } = new();
}

public class ExecutionInfo
{
    public string FunctionName { get; set; }
    public DateTime Date { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public TimeSpan? ExecutionTime { get; set; }
    public TimeSpan? LoadingTime { get; set; }
    public bool IsSuccess { get; set; }
    public string Status { get; set; }
    public string AssemblyId { get; set; }
}
