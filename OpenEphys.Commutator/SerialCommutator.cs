using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Bonsai;

namespace OpenEphys.Commutator
{
    /// <summary>
    /// An operator that controls an Open Ephys commutator by writing a JSON-encoded motion command for each
    /// element of the input sequence to the commutator and produces the JSON command sequence.
    /// </summary>
    [Description("Controls an Open Ephys commutator using a serial port.")]
    public class SerialCommutator : Combinator<double, string>
    {

        readonly BehaviorSubject<bool> enabled = new(true);
        readonly BehaviorSubject<bool> led = new(true);

        /// <summary>
        /// Gets or sets the name of the serial port.
        /// </summary>
        [Category(Definitions.ConfigurationCategory)]
        [TypeConverter(typeof(PortNameConverter))]
        [Description("The name of the serial port.")]
        public string PortName { get; set; }

        /// <summary>
        /// Gets or sets the commutator enable state.
        /// </summary>
        /// <remarks>
        /// If true, the commutator will activate the motor and respond to turn commands. If false,
        /// the motor driver will be deactivated and motion commands will be ignored.
        /// </remarks>
        [Category(Definitions.AcquisitionCategory)]
        [Description("If true, the commutator will be enabled. If false, it will disable the motor and ignore motion commands.")]
        public bool Enable
        {
            get => enabled.Value;
            set => enabled.OnNext(value);
        }

        /// <summary>
        /// Gets or sets the commutator indication LED enable state.
        /// </summary>
        /// <remarks>
        /// If true, the commutator indication LED turn on. If false, the indication LED will turn
        /// off.
        /// </remarks>
        [Category(Definitions.AcquisitionCategory)]
        [Description("If true, the commutator indication LED turn on. If false, the indication LED will turn off.")]
        public bool EnableLed
        {
            get => led.Value;
            set => led.OnNext(value);
        }

        /// <summary>
        /// Writes a JSON-encoded turn command for each element of the input sequence, as well as
        /// configuration property values, to a serial port.
        /// </summary>
        /// <remarks>
        /// A turn command will not be produced for input values that are is NaN or infinity.
        /// </remarks>
        /// <param name="source">A sequence of motor turn values in units of full rotations.</param>
        /// <returns>A sequence of JSON-encoded commands sent to the commutator.</returns>
        public override IObservable<string> Process(IObservable<double> source)
        {
            return Observable.Using(
                () =>
                {
                    var s = new SerialPort(PortName);
                    s.Open();
                    return s;
                },
                s =>
                {
                    var turnCommands = source.Where(x => !double.IsNaN(x) && !double.IsInfinity(x) && x != 0).Select(x => $"{{turn:{x}}}");
                    var enabledCommands = enabled.Select(x => x ? "true" : "false").Select(x => $"{{enable:{x}}}");
                    var ledCommands = led.Select(x => x ? "true" : "false").Select(x => $"{{led:{x}}}");
                    return turnCommands
                        .Merge(enabledCommands)
                        .Merge(ledCommands)
                        .Do(command => s.Write(command));
                }
            );
        }
    }
}
