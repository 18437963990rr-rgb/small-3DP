using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _3DP_KeyLibrary
{
    public class Coordianate
    {
        public double x;
        public double y;
    }

    public class CLIContour
    {
        public CLIContour()
        {
        }
    }

    public class Command
    {
        public enum types
        {
        }
        public List<Coordianate> datas;
        public List<TIFFCommand> tIFFCommands;
        public Command()
        {
        }
    }

    public class IState
    {
        public string state;
    }

    public class Job
    {
        List<Layer> layers;
        public Job()
        {
        }
    }

    public class Jobs
    {
        List<Job> jobs;
        public Jobs()
        {
        }
    }

    public class Layer
    {
        List<Command> commands;
        public Layer()
        {
        }
    }

    public class Parameter
    {
        public Parameter()
        {
        }
        public string name;
        public double value;
    }

    public class Parameters
    {
        List<Parameter> parameters;
        public Parameters()
        {
        }
    }

    public class PlanningParameters
    {
        List<PlanningParameter> planningParameters;
        public PlanningParameters()
        {
        }
    }

    public class PlanningParameter
    {

        public PlanningParameter()
        {
        }
        public enum type { };
        public string name;
        public double value;
    }

    public class PLayer
    {
        List<TIFFCommand> tIFFCommands;
        List<CLIContour> cLIContours;
        public PLayer()
        {
        }
    }

    public class PTask
    {
        List<PLayer> pLayers;
        public PTask()
        {
        }
    }

    public class PTasks
    {
        List<PTask> pTasks;
        public PTasks()
        {
        }
    }

    public class StateData : IState
    {
        public StateData()
        {
        }
    }

    public class TIFFCommand
    {
        public TIFFCommand()
        {
        }
    }
}
