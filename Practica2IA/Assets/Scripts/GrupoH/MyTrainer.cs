using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NavigationDJIA.Interfaces;
using NavigationDJIA.World;
using QMind.Interfaces;
using QMind;
using System.Linq;
using System.Globalization;

namespace GrupoH
{
    public class MyTrainer : IQMindTrainer
    {
        private QMindTrainerParams _parametros;
        private WorldInfo _worldInfo;
        private INavigationAlgorithm _navigationAlgorithm;

        // Tupla de 8 booleanos
        private Dictionary<(bool North, bool South, bool East, bool West, bool FromNorth, bool FromSouth, bool FromEast, bool FromWest), Dictionary<int, float>> _qTable;

        private System.Random _random;
        public int CurrentEpisode { get; private set; }
        public int CurrentStep { get; private set; }
        public CellInfo AgentPosition { get; private set; }
        public CellInfo OtherPosition { get; private set; }
        public float Return { get; private set; }
        public float ReturnAveraged { get; private set; }

        public event EventHandler OnEpisodeStarted;
        public event EventHandler OnEpisodeFinished;

        private string filePath = "Assets/Scripts/GrupoH/TABLAQ_UNITY.csv";

        public void Initialize(QMindTrainerParams qMindTrainerParams, WorldInfo worldInfo, INavigationAlgorithm navigationAlgorithm)
        {
            Application.runInBackground = true;

            _parametros = qMindTrainerParams;
            _worldInfo = worldInfo;
            _navigationAlgorithm = navigationAlgorithm;

            _navigationAlgorithm.Initialize(worldInfo);

            _qTable = new Dictionary<(bool, bool, bool, bool, bool, bool, bool, bool), Dictionary<int, float>>();
            _random = new System.Random();
            CurrentEpisode = 0;
            CurrentStep = 0;
            Return = 0;

            LoadQTable();
            InitializeAllPossibleStates();

            SiguienteEpisodio();
        }

        private void InitializeAllPossibleStates()
        {
            for (int i = 0; i < 256; i++)
            {
                // Extraemos la informacion de los bits
                bool north = (i & 1) != 0;      // Bit 0: CanMoveNorth
                bool south = (i & 2) != 0;      // Bit 1: CanMoveSouth
                bool east = (i & 4) != 0;       // Bit 2: CanMoveEast
                bool west = (i & 8) != 0;       // Bit 3: CanMoveWest

                bool fromNorth = (i & 16) != 0;
                bool fromSouth = (i & 32) != 0;
                bool fromEast = (i & 64) != 0;
                bool fromWest = (i & 128) != 0;

                var state = (north, south, east, west, fromNorth, fromSouth, fromEast, fromWest);

                // Si el estado no existe, lo creamos YA con los castigos por muro
                if (!_qTable.ContainsKey(state))
                {
                    // Pasamos north, south, east, west para saber que direcciones bloquear
                    _qTable[state] = InitializeState(north, south, east, west);
                }
            }
            Debug.Log($"Tabla Q Inicializada completa con {_qTable.Count} estados y penalizaciones por muro.");
        }

        public void DoStep(bool train)
        {
            // Debug.Log($"Total estados distintos en Q-table: {_qTable.Count}"); 

            if (AgenteCazado(AgentPosition))
            {
                OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
                SiguienteEpisodio();
                return;
            }

            if (CurrentEpisode < _parametros.episodes)
            {
                _parametros.epsilon = Mathf.Max(0.1f, 1.0f - ((float)CurrentEpisode / _parametros.episodes));
            }

            if (CurrentEpisode % _parametros.episodesBetweenSaves == 0)
            {
                SaveQTable();
            }

            var state = GetState(AgentPosition);

            int action = SelectAction(state);
            if (action == -1) return;

            Directions direction = (Directions)action;

            if (!IsCardinalDirection(direction)) return;

            CellInfo nextPosition = _worldInfo.NextCell(AgentPosition, direction);

            if (nextPosition == null || !nextPosition.Walkable) return;

            float oldQValue = _qTable[state][action];
            float reward = Recompensa(nextPosition);

            int currentDistance = DistanceToOther(AgentPosition);
            int futureDistance = DistanceToOther(nextPosition);

            if (futureDistance < currentDistance) reward -= 50;
            else reward += 50;

            var nextState = GetState(nextPosition);

            if (!_qTable.ContainsKey(nextState))
            {
                _qTable[nextState] = InitializeState(nextState.Item1, nextState.Item2, nextState.Item3, nextState.Item4);
            }

            float maxFutureQ = _qTable[nextState].Values.Max();

            if (train)
            {
                _qTable[state][action] = oldQValue * (1 - _parametros.alpha) + (_parametros.alpha) * (reward + _parametros.gamma * maxFutureQ);
            }

            AgentPosition = nextPosition;
            Return += reward;
            CurrentStep++;

            NuevaPosHumano();
        }

        private bool IsCardinalDirection(Directions direction)
        {
            return direction == Directions.Up ||
                   direction == Directions.Down ||
                   direction == Directions.Right ||
                   direction == Directions.Left;
        }

        private void SiguienteEpisodio()
        {
            Return = 0;
            CurrentStep = 0;
            CurrentEpisode++;

            AgentPosition = _worldInfo.RandomCell();
            OtherPosition = _worldInfo.RandomCell();

            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
        }

        private float Recompensa(CellInfo nextPosition)
        {
            if (AgenteCazado(nextPosition))
                return -100;
            if (!nextPosition.Walkable)
                return -100000;
            return 0;
        }

        private (bool, bool, bool, bool, bool, bool, bool, bool) GetState(CellInfo position)
        {
            var nextCellNorth = _worldInfo.NextCell(position, Directions.Up);
            var nextCellSouth = _worldInfo.NextCell(position, Directions.Down);
            var nextCellEast = _worldInfo.NextCell(position, Directions.Right);
            var nextCellWest = _worldInfo.NextCell(position, Directions.Left);

            bool canMoveNorth = nextCellNorth != null && nextCellNorth.Walkable;
            bool canMoveSouth = nextCellSouth != null && nextCellSouth.Walkable;
            bool canMoveEast = nextCellEast != null && nextCellEast.Walkable;
            bool canMoveWest = nextCellWest != null && nextCellWest.Walkable;

            bool fromNorth = OtherPosition.y < position.y;
            bool fromSouth = OtherPosition.y > position.y;
            bool fromEast = OtherPosition.x > position.x;
            bool fromWest = OtherPosition.x < position.x;

            return (canMoveNorth, canMoveSouth, canMoveEast, canMoveWest, fromNorth, fromSouth, fromEast, fromWest);
        }

        private int SelectAction((bool, bool, bool, bool, bool, bool, bool, bool) state)
        {

            if (!_qTable.ContainsKey(state))
            {
                _qTable[state] = InitializeState(state.Item1, state.Item2, state.Item3, state.Item4);
            }

            if (_random.NextDouble() < _parametros.epsilon)
            {
                var validActions = _qTable[state].Keys.ToList();
                return validActions[_random.Next(validActions.Count)];
            }
            else
            {
                var bestAction = _qTable[state];
                return MaxByValue(bestAction).Key;
            }
        }

        private Dictionary<int, float> InitializeState(bool canNorth, bool canSouth, bool canEast, bool canWest)
        {
            var state = new Dictionary<int, float>();
            float penalty = -99999f; // Valor muy negativo para muros

            // Si puede moverse (true), peso 0. Si es muro (false), peso -99999.
            state[(int)Directions.Up] = canNorth ? 0.0f : penalty;
            state[(int)Directions.Down] = canSouth ? 0.0f : penalty;
            state[(int)Directions.Right] = canEast ? 0.0f : penalty;
            state[(int)Directions.Left] = canWest ? 0.0f : penalty;

            return state;
        }

        private KeyValuePair<int, float> MaxByValue(Dictionary<int, float> dictionary)
        {
            return dictionary.Aggregate((max, current) => current.Value > max.Value ? current : max);
        }

        private bool AgenteCazado(CellInfo position)
        {
            return position.x == OtherPosition.x && position.y == OtherPosition.y;
        }

        private void NuevaPosHumano()
        {
            try
            {
                CellInfo[] _path = _navigationAlgorithm.GetPath(OtherPosition, AgentPosition, 1);

                if (_path.Length > 0)
                {
                    OtherPosition = _path[0];
                }
            }
            catch (Exception e)
            {
            }
        }

        private int DistanceToOther(CellInfo position)
        {
            return Math.Abs(position.x - OtherPosition.x) + Math.Abs(position.y - OtherPosition.y);
        }

        public void SaveQTable()
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using StreamWriter writer = new StreamWriter(filePath);

                foreach (var state in _qTable)
                {
                    var values = new List<string>
                    {
                        state.Key.Item1.ToString(),
                        state.Key.Item2.ToString(),
                        state.Key.Item3.ToString(),
                        state.Key.Item4.ToString(),
                        state.Key.Item5.ToString(),
                        state.Key.Item6.ToString(),
                        state.Key.Item7.ToString(),
                        state.Key.Item8.ToString()
                    };

                    values.Add(state.Value.ContainsKey((int)Directions.Up) ? state.Value[(int)Directions.Up].ToString(CultureInfo.InvariantCulture) : "0");
                    values.Add(state.Value.ContainsKey((int)Directions.Down) ? state.Value[(int)Directions.Down].ToString(CultureInfo.InvariantCulture) : "0");
                    values.Add(state.Value.ContainsKey((int)Directions.Right) ? state.Value[(int)Directions.Right].ToString(CultureInfo.InvariantCulture) : "0");
                    values.Add(state.Value.ContainsKey((int)Directions.Left) ? state.Value[(int)Directions.Left].ToString(CultureInfo.InvariantCulture) : "0");

                    writer.WriteLine(string.Join(";", values));
                }

                // Debug.Log($"Tabla Q guardada correctamente en {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error al guardar la tabla Q: {ex.Message}");
            }
        }

        public void LoadQTable()
        {
            if (!File.Exists(filePath))
            {
                Debug.Log("No se encontro un archivo de tabla Q existente. Se creara una nueva completa.");
                return;
            }

            using StreamReader reader = new StreamReader(filePath);
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(';');
                if (parts.Length < 12) continue; // Evitar lineas corruptas

                bool north = bool.Parse(parts[0]);
                bool south = bool.Parse(parts[1]);
                bool east = bool.Parse(parts[2]);
                bool west = bool.Parse(parts[3]);
                bool fromNorth = bool.Parse(parts[4]);
                bool fromSouth = bool.Parse(parts[5]);
                bool fromEast = bool.Parse(parts[6]);
                bool fromWest = bool.Parse(parts[7]);

                int actionUp = (int)Directions.Up;
                int actionDown = (int)Directions.Down;
                int actionRight = (int)Directions.Right;
                int actionLeft = (int)Directions.Left;

                float qUp = float.Parse(parts[8], CultureInfo.InvariantCulture);
                float qDown = float.Parse(parts[9], CultureInfo.InvariantCulture);
                float qRight = float.Parse(parts[10], CultureInfo.InvariantCulture);
                float qLeft = float.Parse(parts[11], CultureInfo.InvariantCulture);

                var state = (north, south, east, west, fromNorth, fromSouth, fromEast, fromWest);

                if (!_qTable.ContainsKey(state))
                {
                    _qTable[state] = InitializeState(north, south, east, west);
                }

                _qTable[state][actionUp] = qUp;
                _qTable[state][actionDown] = qDown;
                _qTable[state][actionRight] = qRight;
                _qTable[state][actionLeft] = qLeft;
            }
        }
    }
}