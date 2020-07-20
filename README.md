# ASP.Net Core 3 Starter for Camunda Cloud

Add a file `CamundaCloud.env` in the root of the project, and paste in your client connection credentials from the Camunda Cloud console.

Remove the `export` from the front of each variable.

Then run the project with:

```bash
dotnet run
```
Endpoints:

* [https://localhost:5001/status](https://localhost:5001/status) - Retrieve the status of the cluster.
* [https://localhost:5001/start](https://localhost:5001/start) - Start a workflow instance.
