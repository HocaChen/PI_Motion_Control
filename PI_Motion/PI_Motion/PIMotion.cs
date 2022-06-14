using System;
using System.Text;
using System.Threading;
using PI;
namespace PI_Motion
{
    public class PIMotion
    {
        private static PIController _pi;
        private static Issue1 _issue1;

        static void Main(string[] args)
        {
            _pi = new PIController();
            _pi.Init();

            _issue1 = new Issue1(_pi.DeviceId);
            //_issue1.Issue1_start();
        }
    }

    public class PIController
    {
        public struct AxisStatus
        {
            public bool IsMoving;
            public bool IsPlusLimitOn;
            public bool IsMinusLimitOn;
            public bool IsHomeSensorOn;
            public bool IsServoAlarm;
            public bool IsZPhaseIndexOn;
            public bool IsServoOn;
            public bool IsWaitInp;
            public bool IsInpOn;
            public bool IsEmg;
            public bool IsSwPlusLimitOn;
            public bool IsSwMinusLimitOn;
            public bool IsInitial;
            public bool IsSoftwareLimitOn;
            public double CurrentPos;
            public double EncoderValue;
            public double CurrentSpeed;
            public int ErrorCode;
            public string ErrorMessage;
        }
        private string _id = "[SiPhAxisDriver]";
        private const int PI_RESULT_FAILURE = 0;

        private readonly string[] _axis =
            {"X", "Y", "Z"};

        public int DeviceId { get; private set; }

        public void Init()
        {
            OpenConnection("192.168.0.3");
            PrintControllerIdentification();

            Thread t1 = new Thread(PollingStatus);
            t1.Start();
        }

        private void PollingStatus(object obj)
        {
            while (true)
            {
                Thread.Sleep(100);

                AxisStatus status;
                foreach (var axis in _axis)
                {
                    UpdateAxisStatus(axis, out status);
                    Console.WriteLine($"Axis, {axis}, position:{status.CurrentPos}");
                }
            }
        }

        private void OpenConnection(string hostName = "localhost")
        {
            Console.WriteLine("-- Try to Connect --");
            DeviceId = PiGcs2.ConnectTCPIP(hostName, 50000);

            if (0 <= DeviceId) return;

            throw new Exception("Connection fail");
        }

        private void PrintControllerIdentification()
        {
            var controllerIdentification = new StringBuilder(1024);

            if (PiGcs2.qIDN(DeviceId, controllerIdentification, controllerIdentification.Capacity) ==
                PI_RESULT_FAILURE)
            {
                throw new Exception("qIDN failed.");
            }

            Console.WriteLine("qIDN returned: " + controllerIdentification);
        }

        private void UpdateAxisStatus(string axis, out AxisStatus status)
        {
            int[] axisStatus = new int[2];
            var ret = 0;
            AxisStatus _status = new AxisStatus();
            try
            {
                //---Is Moveing---//
                ret = PiGcs2.IsMoving(DeviceId, axis, axisStatus);
                _status.IsMoving = axisStatus[0] == 1;
                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");


                //status = _status;
                //return EAxisCommanderUnitResult.E_ACS_PASS;

                //--Is inPosition--//
                ret = PiGcs2.qONT(DeviceId, axis, axisStatus);
                _status.IsInpOn = axisStatus[0] == 1;
                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");


                ////---Limit switch---//
                ret = PiGcs2.qLIM(DeviceId, axis, axisStatus);
                _status.IsMinusLimitOn = axisStatus[0] == 1;
                _status.IsPlusLimitOn = axisStatus[1] == 1;
                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");

                //---Home sensor---//
                // Not support

                //---Servo Alarm---//
                var err = 0;
                ret = PiGcs2.qERR(DeviceId, ref err);
                if (err != 0)
                {
                    //--Skip case--//
                    if (err == 1005)
                    {
                        err = 0;
                    }
                }
                _status.ErrorCode = err;
                _status.IsServoAlarm = err != 0;

                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");

                //---IsZPhaseIndexOn---//
                // Not support

                //---Servo On---//
                ret = PiGcs2.qSVO(DeviceId, axis, axisStatus);
                _status.IsServoOn = axisStatus[0] == 1;
                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");

                //---Encoder Pos---//
                double[] curPos = new double[2];
                ret = PiGcs2.qPOS(DeviceId, axis, curPos);
                    _status.EncoderValue = curPos[0];

                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");

                //---Current (Command) Pos---//
                ret = PiGcs2.qMOV(DeviceId, axis, curPos);
                _status.CurrentPos = curPos[0];
                if (ret < 0)
                    throw new Exception(_id + $"error :{ret}");

                //---Current Speed---//
                _status.CurrentSpeed = 0;

                //---Is axis initial-----Only happen in Hexapod Axis//
                    int[] isInit = new int[1];
                    ret = PiGcs2.qFRF(DeviceId, axis, isInit);
                    _status.IsInitial = isInit[0] == 1;
                    if (ret < 0)
                        throw new Exception(_id + $"error :{ret}");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            status = _status;
        }
    }

    public class Issue1
    {
        private int _deviceId;

        public Issue1(int deviceId)
        {
            _deviceId = deviceId;

            SendCommand("HLP?");
            StringBuilder s = new StringBuilder();
            PiGcs2.GcsGetAnswer(_deviceId, s, 4098);
        }

        public void Issue1_start()
        {
            for (int i = 0; i < 1000; i++)
            {
                UpdateCoordinate();
            }
        }

        private void UpdateCoordinate()
        {
            SendCommand("KEN ZERO");
            SendCommand("KSD TIP1S X -116 Y -0.316 Z -92.481");
            SendCommand("KLN TIP1S SENDER");
            SendCommand("KEN TIP1S");
            SendCommand("WPA SKS");
        }

        private void SendCommand(string arg)
        {
            PiGcs2.GcsCommandset(_deviceId, arg);
        }
    }
}
