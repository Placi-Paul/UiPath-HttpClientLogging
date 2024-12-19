using System;
using System.Threading.Tasks;
using UiPath.CodedWorkflows;

namespace HttpClientLogging
{
    public class SampleWorkflow : CodedWorkflow
    {
        [Workflow]
        public async Task Execute()
        {
            var client = CustomHttpClient; //This is required because it uses our custom httpclient with logging

            var response = await client.GetAsync(@"https://dummyjson.com/products");

            var data = await response.Content.ReadAsStringAsync();
            
            Log($"Content Length: {data.Length}");
            
            try
            {
            await client.GetAsync(@"https://some-error.com");
            }
            catch (Exception ex)
            {
                Log("Expected error: " + ex);
            }
        }
    }
}