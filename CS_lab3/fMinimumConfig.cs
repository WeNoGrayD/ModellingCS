using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CS_lab3
{
    public partial class fMinimumConfig : Form
    {
        public frmDCS DCS;

        /* Интенсивность потока заявок на решение средней задачи;
           средняя трудоёмкость решения средней задачи. */

        float lambdaSum = 0,
              averageTeta = 0,
              sumD = 0,
              averageTetaByCalc = 0,
              taskExitP = 0;

        public static int nFiles = 10;

        public int[,] FilesDsByQueries =
        {
            { 0, 0, 0, 5, 8, 7, 6, 0, 0, 9 },
            { 0, 2, 0, 6, 0, 6, 8, 0, 0, 4 },
            { 0, 12, 7, 2, 7, 3, 0, 0, 0, 0 },
            { 0, 0, 3, 5, 7, 9, 1, 0, 0, 0 },
            { 0, 6, 0, 4, 10, 2, 0, 0, 9, 0 }
        };

        public struct FileParams
        {
            public float FileLength, 
                         averageRecordLength;
        }

        List<FileParams> filesParams = new List<FileParams>
        {
            new FileParams{ FileLength = 5, averageRecordLength = 10 },
            new FileParams{ FileLength = 6, averageRecordLength = 9 },
            new FileParams{ FileLength = 7, averageRecordLength = 8 },
            new FileParams{ FileLength = 8, averageRecordLength = 7 },
            new FileParams{ FileLength = 9, averageRecordLength = 6 },
            new FileParams{ FileLength = 10, averageRecordLength = 5 },
            new FileParams{ FileLength = 9, averageRecordLength = 6 },
            new FileParams{ FileLength = 8, averageRecordLength = 7 },
            new FileParams{ FileLength = 7, averageRecordLength = 8 },
            new FileParams{ FileLength = 6, averageRecordLength = 9 },
        };

        public float[] FilesDs = new float[nFiles],
                       FilesUsingPs = new float[nFiles];

        public float minB, optimalB;

        public enum devices
        {
            HD,
            ST,
            CK
        };

        public class DeviceParams
        {
            public float lambda, D, P, L, U;
            public int Z;
        }

        public Dictionary<devices, float> devicesAverageAccessTime,
                                          devicesDataTransitionSpeed,
                                          devicesStorageCapacity,
                                          typicalDevicesCost;

        public Dictionary<devices, DeviceParams> DevicesParams;

        public const float processorCostCoefficient = 4.5F;

        public Dictionary<devices, List<int>> filesPlacement;

        float averageDataTransitionSpeedCK = 0;

        float minU = 0,
              U_Processor = 0;

        float minOPS_Cost = 0;

        public fMinimumConfig(frmDCS dcs)
        {
            InitializeComponent();

            DCS = dcs;
            optimalB = DCS.optimalB;

            InitDevices();

            CalculateAverages();

            CalculateMinimalB();

            DefineFilesPlacementPossibilityOnDevices();

            DefinePeripheralDevicesQuantity();

            DefineCKsQuantity();

            CalculateAverageResponseTime();

            CalculateMinOPS_Cost();

            PrintResults();
        }

        /* Метод InitDevices выполняет инициализацию словарей
          с параметрами устройств и всего с ними связанного. */

        public void InitDevices()
        {
            devicesAverageAccessTime = new Dictionary<devices, float>
            {
                { devices.HD, 0.1F },
                { devices.ST, 2.5F }
            };

            devicesDataTransitionSpeed = new Dictionary<devices, float>
            {
                { devices.HD, 150 },
                { devices.ST, 70 }
            };

            devicesStorageCapacity = new Dictionary<devices, float>
            {
                { devices.HD, 5 },
                { devices.ST, 18 }
            };

            typicalDevicesCost = new Dictionary<devices, float>
            {
                { devices.HD, 60 },
                { devices.ST, 45 },
                { devices.CK, 110 }
            };

            filesPlacement = new Dictionary<devices, List<int>>
            {
                { devices.HD, null },
                { devices.ST, null }
            };

            DevicesParams = new Dictionary<devices, DeviceParams>
            {
                { devices.HD, new DeviceParams()},
                { devices.ST, new DeviceParams()},
                { devices.CK, new DeviceParams()}
            };
        }

        /* Метод CalculatingAverages вычисляет средние значения
           параметров средней задачи. */

        public void CalculateAverages()
        {
            lambdaSum = DCS.bestCS.Iterations[DCS.optimalB_ind].lambdaSum / frmDCS.n;

            /* Вычисление среднего количества обращений к i-му файлу. */

            for (int i = 0; i < nFiles; i++)
            {
                FilesDs[i] = 0;

                for (int j = 0; j < frmDCS.n; j++)
                {
                    FilesDs[i] += frmDCS.lambdas[j] * FilesDsByQueries[j, i];
                }

                FilesDs[i] /= lambdaSum;
            }

            /* Вычисление средней трудоёмкости. */

            for (int i = 0; i < frmDCS.n; i++)
            {
                averageTeta += frmDCS.lambdas[i] * frmDCS.Tetas[i];
            }

            averageTeta /= lambdaSum;

            /* Вычисление суммарное количества обращений к файлам. */

            for (int i = 0; i < nFiles; i++)
            {
                sumD += FilesDs[i];
            }

            /* Вычисление вероятности использования i-го файла
               при решении задач. */

            for (int i = 0; i < nFiles; i++)
            {
                FilesUsingPs[i] = FilesDs[i] / (sumD + 1);
            }

            /* Вычисление средней трудоёмкости этапа счёта. */

            averageTetaByCalc = averageTeta / (sumD + 1);

            /* Вычисление вероятности выхода задачи из системы. */

            taskExitP = 1 / (sumD + 1);
        }

        /* Метод CalculateMinimalB выполняет вычисление 
           минимального быстродействия процессора. */

        public void CalculateMinimalB()
        {
            /* Вычисление минимального быстродействия процессора. */

            minB = 1.1F * lambdaSum * averageTeta; 
        }

        /* Метод DefineFilesPlacementPossibilityOnDevices выполняет определение 
           возможности размещения файлов на различных устройствах. */

        public void DefineFilesPlacementPossibilityOnDevices()
        {
            float fileLambda, fileAccessTime;

            List<int> filesOnHD = new List<int>(),
                      filesOnST = new List<int>();

            /* Распределение файлов по устройствам. */

            for (int i = 0; i < nFiles; i++)
            {
                /* Интенсивность обращения к файлу. */

                fileLambda = lambdaSum * FilesDs[i];

                /* Предельное время доступа к файлу. */

                fileAccessTime = 1 / fileLambda;

                if (FilesDs[i] != 0)
                {
                    if (fileAccessTime < devicesAverageAccessTime[devices.ST])
                        filesOnHD.Add(i);
                    else
                        filesOnST.Add(i);
                }
            }

            filesPlacement[devices.HD] = filesOnHD;
            filesPlacement[devices.ST] = filesOnST;
        }

        /* Метод DefinePeripheralDevicesQuantity выполняет определение 
           количества внешних устройств. */

        public void DefinePeripheralDevicesQuantity()
        {
            float Z_buf;

            int Z_HD_ByR = 0,
                Z_ST_ByR = 0,
                Z_HD_ByCapacity = 0,
                Z_ST_ByCapacity = 0;

            /* Вычисление количества обращений к HD. */

            foreach (int i in filesPlacement[devices.HD])
                DevicesParams[devices.HD].D += FilesDs[i];

            /* Вычисление количества обращений к ST. */

            foreach (int i in filesPlacement[devices.ST])
                DevicesParams[devices.ST].D += FilesDs[i];

            /* Вычисление интенсивности обращений к HD. */

            DevicesParams[devices.HD].lambda = lambdaSum * DevicesParams[devices.HD].D;

            /* Вычисление интенсивности обращений к ST. */

            DevicesParams[devices.ST].lambda = lambdaSum * DevicesParams[devices.ST].D;

            /* Вычисление количества HD в зависимости от максимальной загрузки. */

            Z_HD_ByR = (int)Math.Ceiling
                (DevicesParams[devices.HD].lambda * devicesAverageAccessTime[devices.HD]);

            /* Вычисление количества ST в зависимости от максимальной загрузки. */

            Z_ST_ByR = (int)Math.Ceiling
                (DevicesParams[devices.ST].lambda * devicesAverageAccessTime[devices.ST]);

            /* Вычисление количества HD в зависимости от требуемой ёмкости. */

            Z_buf = 0;

            foreach (int i in filesPlacement[devices.HD])
                Z_buf += 
                    (filesParams[i].FileLength / 
                    devicesStorageCapacity[devices.HD]);

            Z_HD_ByCapacity = (int)Math.Ceiling(Z_buf);

            /* Вычисление количества ST в зависимости от требуемой ёмкости. */

            Z_buf = 0;

            foreach (int i in filesPlacement[devices.ST])
                Z_buf += 
                    (filesParams[i].FileLength /
                    devicesStorageCapacity[devices.ST]);

            Z_ST_ByCapacity = (int)Math.Ceiling(Z_buf);

            /* Определение количества HD по максимальному значению
               Z(HD).*/

            if (Z_HD_ByR > Z_HD_ByCapacity)
                DevicesParams[devices.HD].Z = Z_HD_ByR;
            else
                DevicesParams[devices.HD].Z = Z_HD_ByCapacity;

            /* Определение количества ST по максимальному значению
               Z(ST).*/

            if (Z_ST_ByR > Z_ST_ByCapacity)
                DevicesParams[devices.ST].Z = Z_ST_ByR;
            else
                DevicesParams[devices.ST].Z = Z_ST_ByCapacity;
        }

        /* Метод DefineCKsQuantity выполняет определение 
           количества селекторных каналов. */

        public void DefineCKsQuantity()
        {
            /* Вычисление интенсивности обращений к селекторным каналам. */

            DevicesParams[devices.CK].lambda = lambdaSum * sumD;

            /* Селекторный канал имеет количество обращений,
               равное среднему числу обращений к файлам. */

            DevicesParams[devices.CK].D = sumD;

            /* Вычисление вероятности обращения к HD. */

            foreach (int i in filesPlacement[devices.HD])
                DevicesParams[devices.HD].P += FilesUsingPs[i];

            /* Вычисление вероятности обращения к ST. */

            foreach (int i in filesPlacement[devices.ST])
                DevicesParams[devices.ST].P += FilesUsingPs[i];

            /* Вычисление средней длины записи при обращении к HD. */

            foreach (int i in filesPlacement[devices.HD])
            {
                DevicesParams[devices.HD].L +=
                    filesParams[i].averageRecordLength * FilesUsingPs[i];
                DevicesParams[devices.HD].L /= DevicesParams[devices.HD].P;
            }

            /* Вычисление средней длины записи при обращении к ST. */

            foreach (int i in filesPlacement[devices.ST])
            {
                DevicesParams[devices.ST].L +=
                    filesParams[i].averageRecordLength * FilesUsingPs[i];
                DevicesParams[devices.ST].L /= DevicesParams[devices.ST].P;
            }

            /* Вычисление среднего времени передачи записи 
               через селекторный канал. */

            averageDataTransitionSpeedCK =
                DevicesParams[devices.HD].L * DevicesParams[devices.HD].P / devicesDataTransitionSpeed[devices.HD];
            averageDataTransitionSpeedCK +=
                DevicesParams[devices.ST].L * DevicesParams[devices.ST].P / devicesDataTransitionSpeed[devices.ST];

            /* Вычисление количества CK. */

            DevicesParams[devices.CK].Z = (int)Math.Ceiling(DevicesParams[devices.CK].lambda * averageDataTransitionSpeedCK);
        }

        /* Метод CalculateAverageResponseTime выполняет вычисление
           среднего времени ответа системы оперативной обработки. */

        public void CalculateAverageResponseTime()
        {
            float T_HD = devicesAverageAccessTime[devices.HD],
                  T_ST = devicesAverageAccessTime[devices.ST],
                  T_CK = averageDataTransitionSpeedCK;

            /* Вычисление среднего времени пребывания
               средней заявки на процессоре. */

            U_Processor = averageTeta / (optimalB - lambdaSum * averageTeta);

            /* Вычисление среднего времени пребывания
               данных на HD. */

            if (DevicesParams[devices.HD].Z != 0)
                DevicesParams[devices.HD].U =
                    T_HD /
                    (1 - DevicesParams[devices.HD].lambda * T_HD / DevicesParams[devices.HD].Z);

            /* Вычисление среднего времени пребывания
               данных на ST. */

            if (DevicesParams[devices.ST].Z != 0)
                DevicesParams[devices.ST].U =
                    T_ST / 
                    (1 - DevicesParams[devices.ST].lambda * T_ST / DevicesParams[devices.ST].Z);

            /* Вычисление среднего времени пребывания
               данных в CK. */

            DevicesParams[devices.CK].U =
                T_CK / 
                (1 - DevicesParams[devices.CK].lambda * T_CK / DevicesParams[devices.CK].Z);

            /* Вычисление среднего времени ответа СОО. */

            minU = U_Processor + 
                   DevicesParams[devices.HD].D * DevicesParams[devices.HD].U + 
                   DevicesParams[devices.ST].D * DevicesParams[devices.ST].U +
                   sumD * DevicesParams[devices.ST].U;
        }

        /* Метод CalculateMinOPS_Cost выполняет вычисление
           стоимости СОО с минимальной конфигурацией. */

        public void CalculateMinOPS_Cost()
        {
            minOPS_Cost = processorCostCoefficient * optimalB +
                          DevicesParams[devices.HD].Z * typicalDevicesCost[devices.HD] +
                          DevicesParams[devices.ST].Z * typicalDevicesCost[devices.ST] +
                          DevicesParams[devices.CK].Z * typicalDevicesCost[devices.CK];
        }

        /* Метод PrintResults выполняет вывод данных. */

        public void PrintResults()
        {
            ListView lstv;
            ListViewItem lvi;
            string format2 = "F2",
                   format3 = "F3",
                   format4 = "F4";
            string[] devicesStrings = new string[]
                { "HD", "ST", "CK" };

            string[] averageParams =
            {
                lambdaSum.ToString(format4),
                sumD.ToString(format4),
                averageTeta.ToString(format4),
                averageTetaByCalc.ToString(format4),
                (taskExitP * 100).ToString(format2) + "%"
            };

            lvi = new ListViewItem(averageParams);

            lstvParamsAverage.Items.Add(lvi);

            for (int i = 0; i < nFiles; i++)
            {
                string[] fileParams = new string[]
                    { (i + 1).ToString(), FilesDs[i].ToString() };

                lvi = new ListViewItem(fileParams);

                lstvParamsFiles.Items.Add(lvi);
            }

            foreach (string deviceStr in devicesStrings)
            {
                devices dev = 0;

                if (!deviceStr.Equals("CK"))
                {
                    if (deviceStr.Equals("HD"))
                        dev = devices.HD;
                    else
                        dev = devices.ST;

                    string[] _deviceParams = new string[]
                    {
                        DevicesParams[dev].lambda.ToString(format4),
                        DevicesParams[dev].D.ToString(format4),
                        (DevicesParams[dev].P * 100).ToString(format2) + "%",
                        DevicesParams[dev].L.ToString(format4),
                    };

                    lvi = new ListViewItem(_deviceParams);

                    lstv = (ListView)this.Controls
                        .Find("lstvParams" + deviceStr, true)[0];

                    lstv.Items.Add(lvi);

                    string placedFiles = "";

                    foreach (int file in filesPlacement[dev])
                    {
                        placedFiles += (file + 1).ToString() + " ";
                    }
                    if (placedFiles.Length > 0)
                        placedFiles.Remove(placedFiles.Length - 1);
                    else
                        placedFiles = "--";

                    string[] deviceFiles = new string[]
                    {
                        placedFiles,
                        DevicesParams[dev].U.ToString(format4)
                    };

                    lvi = new ListViewItem(deviceFiles);

                    lstv = (ListView)this.Controls
                        .Find("lstvFiles" + deviceStr, true)[0];

                    lstv.Items.Add(lvi);
                }
                else
                {
                    dev = devices.CK;

                    string[] _deviceParams = new string[]
                    {     
                        DevicesParams[dev].lambda.ToString(format4),
                        DevicesParams[dev].D.ToString(format4),
                    };

                    lvi = new ListViewItem(_deviceParams);

                    lstv = (ListView)this.Controls
                        .Find("lstvParams" + deviceStr, true)[0];

                    lstv.Items.Add(lvi);

                    string[] _onlyCK = new string[]
                    {
                        averageDataTransitionSpeedCK.ToString(format4),
                        DevicesParams[dev].U.ToString(format4)
                    };

                    lvi = new ListViewItem(_onlyCK);

                    lstv = (ListView)this.Controls
                        .Find("lstvOnlyCK", true)[0];

                    lstv.Items.Add(lvi);
                }
                
                string[] _devices = new string[]
                {
                    DevicesParams[dev].Z.ToString(),
                    (DevicesParams[dev].Z * typicalDevicesCost[dev]).ToString(format2)
                };

                lvi = new ListViewItem(_devices);

                lstv = (ListView)this.Controls
                    .Find("lstvDevices" + deviceStr, true)[0];

                lstv.Items.Add(lvi);
            }

            string[] processorParams = new string[]
                {
                    minB.ToString(format3),
                    optimalB.ToString(format3),
                    U_Processor.ToString(format4)
                };

            lvi = new ListViewItem(processorParams);

            lstvParamsProcessor.Items.Add(lvi);

            string[] OPS_Params = new string[]
                {
                    minU.ToString(format4),
                    minOPS_Cost.ToString(format2)
                };

            lvi = new ListViewItem(OPS_Params);

            lstvParamsOPS.Items.Add(lvi);
        }
    }
}
