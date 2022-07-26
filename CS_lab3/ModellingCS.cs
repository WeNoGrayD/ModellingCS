using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ZedGraph;

namespace CS_lab3
{
    /* Перечисление аббревиатур названий ДО. */

    public enum ServiceDisciplines
    {
        WP,
        RP,
        AP,
        MP
    };

    public partial class frmDCS : Form
    {
        /* Структура, содержащая индекс итерации КС
           с минимальным значением функции штрафа < 0.01
           и, собственно, это самое значение функции штрафа. */

        public struct FirstHundreths
        {
            public int Ind;
            public float F;
        }

        /* Класс, содержащая информацию об
           итерации КС с заданным быстродействием B. */

        public class CS_IterationParameters
        {
            /*
               Родительская КС, итерацией коей является данная. 
            */

            CS_ByServiceDiscipline Parent;

            /* 
               Массивы значений: 
               -- реального времени ожидания i-ой заявки;
               -- загрузки, создаваемой i-ой заявкой;
               -- ;
               -- ;
               -- времени пребывания i-ой заявки в процессоре;
               -- относительного времени ожидания i-ой заявки;
               -- вероятности пребывания i-ой заявки в процессоре. 
            */

            public float[] omegas, ros, Vs, V2s, Us, deltaOmegas, Ps;

            /*
               Значения:
               -- суммарной загрузки;
               -- суммарной интенсивности;
               -- длины очереди;
               -- функции штрафа.
            */

            public float R, lambdaSum, l, F;

            /* Конструктор класса*/

            public CS_IterationParameters(CS_ByServiceDiscipline _parent)
            {
                omegas = new float[n];
                ros = new float[n];
                Vs = new float[n];
                V2s = new float[n];
                Us = new float[n];
                deltaOmegas = new float[n];
                Ps = new float[n];

                Parent = _parent;
            }

            /* 
               Метод CalculatingParameters производит вычисление
               параметров компьютерной системы с заданным
               быстродействием.
            */

            public void CalculateParameters(float B)
            {
                lambdaSum = 0;
                R = 0;
                F = 0;
                l = 0;
                for (int i = 0; i < n; i++)
                    omegas[i] = 0;

                int[,] Q = Parent.Q;

                int[] inds_i = Parent.inds_i;
                int[] inds_j = Parent.inds_j;

                int ind1;
                int ind2;

                for (ind1 = 0; ind1 < 5; ind1++)
                {
                    int i = inds_i[ind1];

                    Vs[i] = Tetas[i] / B;
                    V2s[i] = Vs[i] * Vs[i] * (1 + vs[i] * vs[i]);
                    ros[i] = lambdas[i] * Vs[i];
                    lambdaSum += lambdas[i];
                    R += ros[i];
                }

                // Вычисление реального времени ожидания заявки i-го потока.

                for (ind1 = 0; ind1 < 5; ind1++)
                {
                    int i = inds_i[ind1];

                    float buf1, buf2, buf3, buf4 = buf1 = buf2 = buf3 = 0;

                    for (ind2 = 0; ind2 < 5; ind2++)
                    {
                        int j = inds_j[ind2];

                        buf1 += Q[ind2, ind1] * (Q[ind2, ind1] - 1) * ros[j];
                        buf2 += (2 - Q[ind1, ind2]) * (1 + Q[ind1, ind2]) * lambdas[j] * Tetas[j] * Tetas[j] * (1 + vs[j] * vs[j]);
                        buf3 += Q[ind2, ind1] * (3 - Q[ind2, ind1]) * ros[j];
                        buf4 += (1 - Q[ind1, ind2]) * (2 - Q[ind1, ind2]) * ros[j];
                    }

                    buf2 /= B * B * (2 - buf3) * (2 - buf4);

                    omegas[i] += Tetas[i] * buf1;
                    omegas[i] /= B * (2 - buf1);
                    omegas[i] += buf2;

                    Us[i] = omegas[i] + Vs[i];

                    deltaOmegas[i] = omegasAst[i] - omegas[i];

                    Ps[i] = R * (float)Math.Exp(-R * (omegasAst[i] / omegas[i]));

                    F += lambdas[i] * Ps[i];

                    l += lambdas[i] * omegas[i];
                }
            }
        }

        /*
           Класс, содержащий информацию о КС, построенной
           на основе определённой дисциплины обслуживания.
        */

        public class CS_ByServiceDiscipline
        {
            /* Дисциплина обслуживания, по которой работает данная КС. */

            public ServiceDisciplines SD;

            // Матрица приоритетов заявок.

            public int[,] Q;

            /* Массивы индексов строк и столбцов матрицы приоритетов. */

            public int[] inds_i = new int[n];
            public int[] inds_j = new int[n];

            /* Список параметров итераций КС для каждого быстродействия
               в промежутке между минимальным и максимальным с заданным шагом. */

            public List<CS_IterationParameters> Iterations;

            /*
               Массив структур, содержащих индекс и значение
               функции штрафа итерации КС:
               -- с функцией штрафа < 0.01 и минимально возможным быстродейтвием;
               -- с максимальным быстродействием.
            */

            public FirstHundreths[] FHs;

            /* Конструктор класса. */

            public CS_ByServiceDiscipline(ServiceDisciplines _sd)
            {
                SD = _sd;

                Iterations = new List<CS_IterationParameters>();
            }

            /* 
               Метод InitializePriorityMatrix инициализирует
               матрицу приоритетов в соответствии с заданной 
               дисциплиной обслуживания:
            */

            public void InitializePriorityMatrix()
            {
                int minInd;
                float minF;

                FHs = new FirstHundreths[2];

                int[] PriorityRows;

                int[,] PriorityMatrix = new int[n, n];

                if (SD == 0)
                {
                    Q = new int[,]
                            {
                            { 0, 0, 0, 0, 0},
                            { 0, 0, 0, 0, 0},
                            { 0, 0, 0, 0, 0},
                            { 0, 0, 0, 0, 0},
                            { 0, 0, 0, 0, 0},
                            };
                }
                else
                {
                    int[,] QBuf;

                    if ((int)SD < 3)

                    {
                        PriorityRows = new int[n];

                        for (int i = 0; i < n; i++)
                        {
                            PriorityRows[i] = i;
                        }

                        InitPriorityMatrix();

                        SortPriorityMatrix();

                        FHs = SimulateCS();

                        PriorityMatrix = (int[,])Q.Clone();
                        minInd = FHs[0].Ind;
                        minF = FHs[1].F;

                        /* Моделирование КС с перестановками приоритетов строк
                           в матрице приоритетов. */

                        while (RearrangeRows())
                        {
                            InitPriorityMatrix();

                            QBuf = (int[,])Q.Clone();

                            SortPriorityMatrix();

                            FHs = SimulateCS();

                            /* 
                               Сравнение характеристик двух КС с разными
                               матрицами приоритетов.
                               ЕСЛИ индекс итерации новой КС с минимальным значением
                               функции штрафа < 0 меньше такового индекса предыдущей
                               лучшей КС
                               И 
                               ЕСЛИ значение функции штрафа новой КС при максимальном
                               быстродействии меньше таковой у предыдущей лучшей КС
                               ТО новая матрица становится лучшей; индекс итерации
                               с минимальным значением функции штрафа < 0 обновляется,
                               минимальное значение функции штрафа итерации КС 
                               с максимальным быстродействием также обновляется.
                            */

                            if (FHs[1].F < minF && FHs[0].Ind <= minInd)
                            {
                                PriorityMatrix = (int[,])QBuf.Clone();
                                minInd = FHs[0].Ind;
                                minF = FHs[1].F;
                            }
                        }
                    }

                    /* Если ДОСП: */

                    if ((int)SD == 3)
                    {
                        /* Ожидание окончания симуляции КС с ДОАП. */

                        while (true)
                        {
                            lock (frmDCS.locker_CSsAreReady)
                            {
                                if (CSsAreReady[ServiceDisciplines.AP])
                                    break;
                            }
                        }

                        int count = 0;

                        minInd = frmDCS.CSs[ServiceDisciplines.AP].FHs[1].Ind;
                        minF = frmDCS.CSs[ServiceDisciplines.AP].FHs[1].F;

                        /*
                           Копирование матрицы приоритетов и индексов
                           её строк и столбцов из АП.
                        */

                        Q = (int[,])frmDCS.CSs[ServiceDisciplines.AP].Q.Clone();
                        inds_i = (int[])frmDCS.CSs[ServiceDisciplines.AP].inds_i.Clone();
                        inds_j = (int[])frmDCS.CSs[ServiceDisciplines.AP].inds_j.Clone();

                        PriorityMatrix = (int[,])Q.Clone();

                        QBuf = (int[,])Q.Clone();

                        /*
                           Для изменения матрицы АП на СП (2 -> 1)
                           необходимо узнать число размещений с повторениями.
                           А(повт.) = 2 * (число_двоек_в_матрице_АП). 
                           2 - потому что можно поменять двойку на единицу
                           или не менять (бит). 
                        */

                        for (int i = 0; i < Q.GetLength(0); i++)
                            for (int j = 0; j < Q.GetLength(1); j++)
                            {
                                if (Q[i, j] > 0)
                                    count++;
                            }

                        /*
                           Моделирование КС с изменением приоритетов строк
                           в матрице приоритетов в соответствии 
                           с порядковым кодом размещения с повторениями.
                        */

                        for (int code = 1; code < Math.Pow(2, count); code++)
                        {
                            int[] binaryCode = new int[count];

                            /* Получение массива двоичных цифр числа. */

                            int bufCode = code;

                            for (int i = count - 1; i >= 0; i--)
                            {
                                binaryCode[i] = bufCode % 2;
                                bufCode /= 2;
                            }

                            /* Замена двоек в матрице приоритетов на единицы
                               в соответствии с порядковым кодом 
                               размещения с повторениями. */

                            int k = 0;

                            for (int i = 0; i < Q.GetLength(0); i++)
                                for (int j = 0; j < Q.GetLength(1); j++)
                                {
                                    if (Q[i, j] > 0)
                                    {
                                        Q[i, j] = Math.Abs(Q[i, j] - binaryCode[k]);
                                        k++;
                                    }

                                    if (k == count)
                                        break;
                                }

                            FHs = SimulateCS();

                            /* 
                               Сравнение характеристик двух КС с разными
                               матрицами приоритетов.
                               ЕСЛИ индекс итерации новой КС с минимальным значением
                               функции штрафа < 0 меньше такового индекса предыдущей
                               лучшей КС
                               И 
                               ЕСЛИ значение функции штрафа новой КС при максимальном
                               быстродействии меньше таковой у предыдущей лучшей КС
                               ТО новая матрица становится лучшей; индекс итерации
                               с минимальным значением функции штрафа < 0 обновляется,
                               минимальное значение функции штрафа итерации КС 
                               с максимальным быстродействием также обновляется.
                            */

                            if (FHs[1].F < minF && FHs[0].Ind <= minInd)
                            {
                                PriorityMatrix = (int[,])Q.Clone();
                                minInd = FHs[0].Ind;
                                minF = FHs[1].F;
                            }

                            Q = (int[,])QBuf.Clone();
                        }
                    }
                }

                /* 
                   В глобальную матрицу приоритетов заносится
                   та МП, которая лучше всего себя показала. 
                */

                Q = PriorityMatrix;

                /* 
                   Если не ДОСП, то потребуется сортировка матрицы
                   для получения номеров заявок по индексам строк
                   и столбцов матрицы (ind_i, ind_j).
                */

                if ((int)SD < 3)
                    SortPriorityMatrix();

                /* 
                   В глобальную матрицу приоритетов заносится
                   та МП, которая лучше всего себя показала. 
                */

                FHs = SimulateCS(Iterations);

                // Подтверждение окончания симуляции КС с заданной ДО.

                lock (locker_CSsAreReady)
                    frmDCS.CSsAreReady[this.SD] = true;

                /* Пробная инициализация матрицы приоритетов. */

                void InitPriorityMatrix()
                {
                    Q = new int[n, n];

                    /* mode задаёт дисциплину приоритетов.
                       В СП матрица приоритетов инициализируется
                       аналогично АП. */

                    int q;
                    if ((int)SD == 3)
                        q = 2;
                    else
                        q = (int)SD;

                    for (int i = 0; i < Q.GetLength(0); i++)
                    {
                        /* k - индекс номера i-той заявки в массиве
                           приоритетов строк PriorityRows. */

                        int k = Array.FindIndex(PriorityRows, r => r == i);

                        for (int j = 0; j < Q.GetLength(0); j++)
                        {
                            /* l - индекс номера j-той заявки в списке
                               строк приоритетов PriorityRows. */

                            int l = Array.FindIndex(PriorityRows, r => r == j);

                            /* Если k > l, то i-ая заявка имеет больший приоритет
                               перед j-ой заявкой, иначе её приоритет меньше 
                               или она сравнивается сама с собой. */

                            if (k < l)
                                Q[i, j] = q;
                            else
                                Q[i, j] = 0;
                        }
                    }
                }

                /* Обмен приоритетами в массиве приоритетов строк. */

                void SwapRows(int i, int j)
                {
                    int buf = PriorityRows[i];
                    PriorityRows[i] = PriorityRows[j];
                    PriorityRows[j] = buf;
                }

                /* Метод пересчитывает перестановку в массиве приоритетов
                   лексикографическим способом. Метод возвращает false,
                   если доступных перестановок больше нет. */

                bool RearrangeRows()
                {
                    int j = n - 2;

                    while (j > -1 && PriorityRows[j] >= PriorityRows[j + 1])
                        j--;

                    if (j == -1)
                        return false;

                    int k = n - 1;

                    while (PriorityRows[j] > PriorityRows[k])
                        k--;

                    SwapRows(j, k);

                    int l = j + 1, r = n - 1;

                    while (l < r)
                        SwapRows(l++, r--);

                    return true;
                }
            }

            /* Метод SortPriorityMatrix приводит матрицу приоритетов 
           к каноническому виду. */

            public void SortPriorityMatrix()
            {
                int i, j, k, p, z;
                int[] zeros = new int[n];
                bool f;

                // Подсчёт нулей в строках матрицы.

                for (i = 0; i < n; i++)
                {
                    inds_i[i] = i;
                    inds_j[i] = i;

                    for (j = 0; j < n; j++)
                    {
                        if (Q[i, j] == 0)
                        {
                            zeros[i]++;
                        }
                    }
                }

                int buf;

                // Сортировка строк матрицы по количеству нулей.

                for (i = 0; i < n; i++)
                {
                    f = false;
                    for (j = 0; j < n - i - 1; j++)
                    {
                        if (zeros[j] > zeros[j + 1])
                        {
                            // Изменение массива счётчиков нулей.

                            buf = zeros[j];
                            zeros[j] = zeros[j + 1];
                            zeros[j + 1] = buf;

                            buf = inds_i[j];
                            inds_i[j] = inds_i[j + 1];
                            inds_i[j + 1] = buf;

                            // Обмен строками в матрице.

                            for (k = 0; k < n; k++)
                            {
                                buf = Q[j, k];
                                Q[j, k] = Q[j + 1, k];
                                Q[j + 1, k] = buf;
                            }

                            f = true;
                        }
                    }
                    if (f == false)
                        break;
                }

                // Сортировка столбцов в матрице.

                for (i = 0; i < n; i += z + 1)
                {
                    int ind1 = inds_i[i];

                    for (j = 0; j < n; j++)
                    {
                        f = false;
                        for (k = 0; k < n - j - 1; k++)
                        {
                            if (Q[ind1, k] > Q[ind1, k + 1] &&
                                Q[ind1, k + 1] == 0)
                            {
                                // Обмен столбцами в матрице.

                                for (p = 0; p < n; p++)
                                {
                                    buf = Q[p, k];
                                    Q[p, k] = Q[p, k + 1];
                                    Q[p, k + 1] = buf;
                                }

                                buf = inds_j[k];
                                inds_j[k] = inds_j[k + 1];
                                inds_j[k + 1] = buf;

                                f = true;
                            }
                        }
                        if (f == false)
                            break;
                    }

                    z = 0;

                    // Пропуск одинаковых строк матрицы.

                    /* Строки мы отсортировали по количеству нулей. 
                       В одной отдельной строке с индексом ind1 отсортировали
                       столбцы по условию "больше 0". Учитывая, что
                       матрица приоритетов правильная
                       (у двух заявок нет приоритета друг над другом,
                       это вроде бы единственное условие правильности),
                       будут либо одинаковые с этой строки,
                       либо строки с одинаковым количеством нулей
                       (а значащие числа будут отличаться).
                       k = ind1, т.е. мы знаем, что перед диагональным элементом матрицы
                       все нули и он сам ноль. Если следующий элемент в столбце = 0,
                       то пропускаем следующую строку. И так далее. */

                    if (ind1 == i)
                    {
                        for (j = ind1 + 1; j < n; j++)
                        {
                            if (Q[ind1, j] != 0)
                                break;
                            z++;
                        }
                    }
                }
            }

            /* Метод SimulateCS производит моделирование компьютерной системы
               с заданными характеристиками. */

            public FirstHundreths[] SimulateCS()
            {
                float B;

                CS_IterationParameters _iter;

                /* Массив структур "первые сотые": 
                   -- FH1: структура, включающая в себя индекс Ind
                   итерации КС с предположительно меньшим быстродействием
                   (может быть и максимальное, тогда FH1 == FH2) и 
                   значением штрафной функции < 0.01;
                   -- FH2: структура, включающя в себя индекс Ind
                   последней итерации КС с максимальным быстродействием. */

                FirstHundreths FH1 = new FirstHundreths
                { Ind = -1, F = -1 },
                FH2 = new FirstHundreths
                { Ind = 5, F = -1 };

                for (int i = 0; i < Bs.Length; i++)
                {
                    B = Bs[i];

                    _iter = new CS_IterationParameters(this);

                    _iter.CalculateParameters(B);

                    float F = _iter.F;

                    if (FH1.Ind == -1 && (F < 0.01 || i == 5))
                    {
                        FH1.Ind = i;
                        FH1.F = F;
                    }

                    if (i == 5)
                    {
                        FH2.F = F;
                    }
                }

                return new FirstHundreths[] { FH1, FH2 };
            }

            /* Метод SimulateCS производит моделирование компьютерной системы
               с заданными характеристиками. */

            public FirstHundreths[] SimulateCS
                (List<CS_IterationParameters> _iterations)
            {
                float B;

                CS_IterationParameters _iter;

                /* Массив структур "первые сотые": 
                   -- FH1: структура, включающая в себя индекс Ind
                   итерации КС с предположительно меньшим быстродействием
                   (может быть и максимальное, тогда FH1 == FH2) и 
                   значением штрафной функции < 0.01;
                   -- FH2: структура, включающя в себя индекс Ind
                   последней итерации КС с максимальным быстродействием. */

                FirstHundreths FH1 = new FirstHundreths
                { Ind = -1, F = -1 },
                FH2 = new FirstHundreths
                { Ind = 5, F = -1 };

                for (int i = 0; i <= 5; i++)
                {
                    B = Bs[i];

                    _iter = new CS_IterationParameters(this);

                    _iter.CalculateParameters(B);

                    float F = _iter.F;

                    if (FH1.Ind == -1 && (F < 0.01 || i == 5))
                    {
                        FH1.Ind = i;
                        FH1.F = F;
                    }

                    if (i == 5)
                    {
                        FH2.F = F;
                    }

                    _iterations.Add(_iter);
                }

                return new FirstHundreths[] { FH1, FH2 };
            }
        }

        // Количество потоков.

        public static int n = 5;

        /* Индекс оптимального быстродействия в массиве его значений. */

        public int optimalB_ind;

        // Поля, в которых хранятся значения производительности.

        public float Bmin, Bmax, deltaB, optimalB;

        // Массивы времени ожидания, интенсивности поступления задач и времени пребывания.

        public static float[] 
            omegasAst = new float[]{
            2.0F, 6.0F, 0.4F, 0.5F, 3.0F
            },
            lambdas = new float[] {
            1.7F, 1.3F, 0.5F, 1.4F, 1.0F
            },
            vs = new float[] {
            0.95F, 0.85F, 0.85F, 0.80F, 0.80F
            };

        // Массив значений средней трудоёмкости i-го потока. 

        public static int[] Tetas = new int[]
        {
            26700, 19390, 53910, 21020, 51090
        };

        // Список значений производительности для разных итераций КС.

        public static float[] Bs = new float[n + 1];

        /* 
           Словарь "ДО-КС":
           ключ: аббревиатура дисциплины обслуживания;
           значение: объект, вмещающий общую информацию
           о КС с данной ДО в основе и обо всех итерациях
           данной КС.
        */

        public static Dictionary<ServiceDisciplines, CS_ByServiceDiscipline> CSs =
            new Dictionary<ServiceDisciplines, CS_ByServiceDiscipline>();

        /* 
           Словарь "ДО-флаг":
           ключ: аббревиатура дисциплины обслуживания;
           значение: флаг того, что КС подготовлена к состязанию между КС.
        */

        public static Dictionary<ServiceDisciplines, bool> CSsAreReady =
            new Dictionary<ServiceDisciplines, bool>
            {
                { ServiceDisciplines.WP, false },
                { ServiceDisciplines.RP, false },
                { ServiceDisciplines.AP, false },
                { ServiceDisciplines.MP, false }
            };

        /* Строка, содержащая название текущей ДО; 
           строка, содержащая название лучшей ДО. */

        ServiceDisciplines curSD, bestSD = ServiceDisciplines.WP;

        // КС с оптимальной ДО.

        public CS_ByServiceDiscipline bestCS;

        // Локеры.

        static object locker_CSs = new object(),
                      locker_CSsAreReady = new object();

        Dictionary<int, int> dictIndexesOfQueries =
            new Dictionary<int, int>
            {
                { 0, 17 },
                { 1, 21 },
                { 2, 14 },
                { 3, 5 },
                { 4, 24 }
            };

        // Конструктор класса.

        public frmDCS()
        {
            InitializeComponent();

            CalculatePerformance();

            int[,] PriorityMatrix = new int[n, n];

            foreach (ServiceDisciplines SD in ServiceDisciplines.GetValues(typeof(ServiceDisciplines)))
            {
                curSD = SD;
                Thread CS_Thread = new Thread(InitCS);

                switch (SD)
                {
                    case ServiceDisciplines.WP: CS_Thread.Name = "ДОБП"; break;
                    case ServiceDisciplines.RP: CS_Thread.Name = "ДООП"; break;
                    case ServiceDisciplines.AP: CS_Thread.Name = "ДОАП"; break;
                    case ServiceDisciplines.MP: CS_Thread.Name = "ДОСП"; break;
                }

                CS_Thread.Start(curSD);
            }

            while (true)
            {
                lock (CSsAreReady)
                {
                    if (CSsAreReady[ServiceDisciplines.WP] &&
                      CSsAreReady[ServiceDisciplines.RP] &&
                      CSsAreReady[ServiceDisciplines.AP] &&
                      CSsAreReady[ServiceDisciplines.MP])
                        break;
                }
            }

            /* 
               Проход по дисциплинам обслуживание,
               сохранение лучше всего подходящей ДО. 
            */

            int minInd = 0;
            float minF = 0;

            float minD = 0, D;

            foreach (ServiceDisciplines SD in ServiceDisciplines.GetValues(typeof(ServiceDisciplines)))
            {
                curSD = SD;

                CS_ByServiceDiscipline CS = CSs[curSD];

                int curOptimalB_ind = CS.FHs[0].Ind; 

                if (curSD == 0)
                {
                    minD = CheckDeltaOmegas();
                    /*
                    minInd = CS.FHs[0].Ind;
                    minF = CS.FHs[1].F;
                    */
                }
                else
                {
                    D = CheckDeltaOmegas();
                    if (D < minD)
                    {
                        minD = D;
                        bestSD = curSD;
                    }
                    /*
                    if (CS.FHs[1].F < minF && CS.FHs[0].Ind <= minInd)
                    {
                        minInd = CS.FHs[0].Ind;
                        minF = CS.FHs[1].F;

                        bestSD = curSD;
                    }
                    */
                }

                /*
                   Метод CheckDeltaOmegas подсчитывает:
                   -- относительный запас по времени ожидания
                   для каждой заявки в итерации КС
                   с оптимальным быстродействием;
                   -- среднее значение относительного запаса 
                   времени ожидания;
                   -- максимальный разброс между средним значением
                   и конкретным.
                */

                float CheckDeltaOmegas()
                {
                    float averageDeltaOmega = 0, omega;
                    float[] deltaOmegas = new float[n];

                    /*
                       Подсчёт конкретного относительного запаса
                       по времени ожидания для каждой заявки 
                       и среднего значения.
                    */

                    for (int i = 0; i < n; i++)
                    {
                        omega = CS.Iterations[curOptimalB_ind].omegas[i];
                        deltaOmegas[i] = (omegasAst[i] - omega) / omega;
                        averageDeltaOmega += deltaOmegas[i];
                    }

                    /* 
                       Считаем максимальный разброс от среднего значения
                       относительного запаса по времени ожидания. 
                    */

                    float maxD = 0, curD;

                    for (int i = 0; i < n; i++)
                    {
                        curD = Math.Abs(averageDeltaOmega - deltaOmegas[i]);
                        if (curD > maxD)
                            maxD = curD;
                    }

                    return maxD;
                }
            }

            /* Параметры КС выводятся для дисциплины обслуживания
               с лучшими характеристиками. */

            bestCS = CSs[bestSD];

            for (int i = 0; i < Bs.Length; i++)
                PrintResults(i);

            optimalB_ind = CSs[bestSD].FHs[0].Ind;

            /* Округление значения оптимального быстродействия. */

            optimalB = Bs[optimalB_ind];
            optimalB += 50000 - optimalB % 50000;

            switch (bestSD)
            {
                case ServiceDisciplines.WP:
                    {
                        lstvOptimalSD.Items.Add("ДОБП");
                        break;
                    }
                case ServiceDisciplines.RP:
                    {
                        lstvOptimalSD.Items.Add("ДООП");
                        break;
                    }
                case ServiceDisciplines.AP:
                    {
                        lstvOptimalSD.Items.Add("ДОАП");
                        break;
                    }
                case ServiceDisciplines.MP:
                    {
                        lstvOptimalSD.Items.Add("ДОСП");
                        break;
                    }
            }
        }

        /* 
           Метод InitCS предназначен для запуска симуляции КС 
           из отдельного потока. На вход принимает аббревиатуру
           названия ДО, по которой работает симулируемая КС.
        */

        public void InitCS(object SD)
        {
            ServiceDisciplines _curSD = (ServiceDisciplines)SD;

            CS_ByServiceDiscipline CS = new CS_ByServiceDiscipline(_curSD);

            lock (locker_CSs)
            {
                CSs.Add(_curSD, CS);
            }

            CS.InitializePriorityMatrix();
        }

        /* Метод CalculatePerformance подсчитываем минимальное
           и максимальное быстродействия и заносит в массив
           быстродействий эти и промежуточные значения. */

        public void CalculatePerformance()
        {
            // Вычисление минимальной производительности.

            float A = 0, C = 0, D = 0;

            for (int i = 0; i < 5; i++)
            {
                A += lambdas[i] * Tetas[i];
                C += lambdas[i] * Tetas[i] * Tetas[i] * (1 + vs[i] * vs[i]);
                D += lambdas[i] * Tetas[i] * omegasAst[i];
            }

            Bmin = 0.5F * A + (float)Math.Sqrt(0.25F * A * (A + (2 * C / D)));

            Bmax = 2 * Bmin;

            float B = Bmax - Bmin;

            deltaB = B / 5;

            for (int i = 0; i <= 5; i++)
            {
                Bs[i] = B;
                B += deltaB;
            }
        }

        /* Метод PrintResults выводит результаты моделирования
           компьютерной системы на экранную форму. */

        public void PrintResults(int i)
        {
            CS_IterationParameters _iter = bestCS.Iterations[i];

            ListView lv1 = null, lv2 = null, lv3 = null;
            ListViewItem lvi;
            string s1, s2, s3, format2 = "F2", format4 = "F4";
            string[] str;

            s1 = "P" + (i + 1).ToString() + "_T1";
            s2 = "P" + (i + 1).ToString() + "_T2";
            s3 = "P" + (i + 1).ToString() + "_T3";

            foreach (TabPage tp in tabControl1.TabPages)
            {
                foreach (Control c in tp.Controls)
                {
                    if (c.Name.Contains(s1))
                    {
                        lv1 = (ListView)c;
                        if (lv2 != null && lv3 != null)
                            break;
                    }

                    if (c.Name.Contains(s2))
                    {
                        lv2 = (ListView)c;
                        if (lv1 != null && lv3 != null)
                            break;
                    }

                    if (c.Name.Contains(s3))
                    {
                        lv3 = (ListView)c;
                        if (lv1 != null && lv2 != null)
                            break;
                    }
                }
            }

            lvi = new ListViewItem(Bs[i].ToString());

            lv1.Items.Add(lvi);

            for (int k = 0; k < 5; k++)
            {
                str = new String[] 
                {
                    (dictIndexesOfQueries[k]).ToString(),
                    _iter.Vs[k].ToString(format4),
                    _iter.V2s[k].ToString(format4),
                    _iter.ros[k].ToString(format4),
                    _iter.omegas[k].ToString(format4),
                    _iter.Us[k].ToString(format4),
                    _iter.deltaOmegas[k].ToString(format4),
                    (_iter.Ps[k] * 100).ToString(format2) + "%"
                };

                lvi = new ListViewItem(str);

                lv2.Items.Add(lvi);
            }

            str = new String[] { _iter.lambdaSum.ToString(format4),
                                 _iter.R.ToString(format4),
                                 _iter.l.ToString(format4),
                                 _iter.F.ToString(format4) };
            lvi = new ListViewItem(str);
            lv3.Items.Add(lvi);
        }

        /* Параметры СОО с минимальной конфигурацией. */

        private void btnMinimumConfig_Click(object sender, EventArgs e)
        {
            fMinimumConfig fMinCnfg = new fMinimumConfig(this);

            this.Hide();

            fMinCnfg.ShowDialog();

            this.Show();
        }

        /* Рисование графиков. */

        private void btnDiagramByB_Click(object sender, EventArgs e)
        {
            ZedGraphControl zgcOmegasByB = new ZedGraphControl();

            GraphPane paneOmegasByB = zgcOmegasByB.GraphPane;

            PointPairList omegas_B;

            for (int i = 0; i < n; i++)
            {
                omegas_B = new PointPairList();

                Color col = new Color();

                string query = "Заявка " + (dictIndexesOfQueries[i]).ToString();

                switch (i)
                {
                    case 0: { col = Color.Red; break; }
                    case 1: { col = Color.Blue; break; }
                    case 2: { col = Color.Purple; break; }
                    case 3: { col = Color.Pink; break; }
                    case 4: { col = Color.Green; break; }
                    case 5: { col = Color.Yellow; break; }
                }

                for (int j = 0; j <= 5; j++)
                {
                    omegas_B.Add(Bs[j], bestCS.Iterations[j].omegas[i]);
                }

                LineItem queryCurve = paneOmegasByB.AddCurve(query, omegas_B, col);
            }

            paneOmegasByB.Title.Text = "График зависимости времени ожидания заявок от быстродействия";
            paneOmegasByB.XAxis.Title.Text = "Быстродействие КС B";
            paneOmegasByB.YAxis.Title.Text = "Время ожидания заявки ω";

            fDiagramView dvOmegasByB = new fDiagramView(this, zgcOmegasByB);

            this.Hide();

            dvOmegasByB.ShowDialog();

            this.Show();
        }

        private void btnDiagramByPR_Click(object sender, EventArgs e)
        {
            ZedGraphControl zgcOmegasByPR = new ZedGraphControl();

            GraphPane paneOmegasByPR = zgcOmegasByPR.GraphPane;

            PointPairList omegas_PR;

            for (int i = 0; i < CSs.Count; i++)
            {
                omegas_PR = new PointPairList();

                Color col = new Color();

                string SD = "ДО";

                switch (i)
                {
                    case 0: { col = Color.Red; SD += "БП"; break; }
                    case 1: { col = Color.Blue; SD += "ОП"; break; }
                    case 2: { col = Color.Purple; SD += "АП"; break; }
                    case 3: { col = Color.Green; SD += "СП"; break; }
                }

                List<float> omegasSorted = new List<float>();
                for (int j = 0; j < n; j++)
                    omegasSorted.Add(CSs.Values.ToList()[i].Iterations[optimalB_ind].omegas[j]);

                omegasSorted.Sort();

                for (int j = 0; j < n; j++)
                {
                    omegas_PR.Add(j + 1, omegasSorted[j]);
                }

                LineItem queryCurve = paneOmegasByPR.AddCurve(SD, omegas_PR, col);
            }

            paneOmegasByPR.Title.Text = "График зависимости времени ожидания заявок от ДО";
            paneOmegasByPR.XAxis.Title.Text = "Номер заявки по приоритету N";
            paneOmegasByPR.YAxis.Title.Text = "Время ожидания заявки ω";

            fDiagramView dvOmegasByPR = new fDiagramView(this, zgcOmegasByPR);

            this.Hide();

            dvOmegasByPR.ShowDialog();

            this.Show();
        }

        private void btnDiagramByR_Click(object sender, EventArgs e)
        {
            ZedGraphControl zgcOmegasByR = new ZedGraphControl();

            GraphPane paneOmegasByR = zgcOmegasByR.GraphPane;

            PointPairList omegas_R;

            for (int i = 0; i < n; i++)
            {
                omegas_R = new PointPairList();

                Color col = new Color();

                string query = "Заявка " + (dictIndexesOfQueries[i]).ToString();

                switch (i)
                {
                    case 0: { col = Color.Red; break; }
                    case 1: { col = Color.Blue; break; }
                    case 2: { col = Color.Purple; break; }
                    case 3: { col = Color.Pink; break; }
                    case 4: { col = Color.Green; break; }
                    case 5: { col = Color.Yellow; break; }
                }

                for (int j = 0; j <= 5; j++)
                {
                    omegas_R.Add(bestCS.Iterations[j].R, bestCS.Iterations[j].omegas[i]);
                }

                LineItem queryCurve = paneOmegasByR.AddCurve(query, omegas_R, col);
            }

            paneOmegasByR.Title.Text = "График зависимости времени ожидания заявок от загрузки";
            paneOmegasByR.XAxis.Title.Text = "Загрузка КС R";
            paneOmegasByR.YAxis.Title.Text = "Время ожидания заявки ω";

            fDiagramView dvOmegasByR = new fDiagramView(this, zgcOmegasByR);

            this.Hide();

            dvOmegasByR.ShowDialog();

            this.Show();
        }
    }
}
