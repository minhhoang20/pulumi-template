using System.Threading.Tasks;
using Pulumi;
using PulumiTemplate;

public class Program
{
    static Task<int> Main() => Deployment.RunAsync<TemplateStack>();
}
