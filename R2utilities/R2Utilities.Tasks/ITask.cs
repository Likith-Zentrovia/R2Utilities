using R2Library.Data.ADO.R2Utility;

namespace R2Utilities.Tasks;

internal interface ITask
{
	TaskResult TaskResult { get; }

	string TaskName { get; }

	string TaskDescription { get; }

	string TaskSwitch { get; }

	string TaskSwitchSmall { get; }

	TaskGroup TaskGroup { get; }

	string[] CommandLineArguments { get; }

	bool IsEnabled { get; }

	void Init(string[] commandLineArguments);

	void Run();

	void Cleanup();
}
