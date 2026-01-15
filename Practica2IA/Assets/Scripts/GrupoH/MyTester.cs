using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using NavigationDJIA.World;
using QMind.Interfaces;
using System.Globalization;

namespace GrupoH
{
    public class MyTester : IQMind
    {
        private Dictionary<(bool North, bool South, bool East, bool West, bool FromNorth, bool FromSouth, bool FromEast, bool FromWest), Dictionary<int, float>> _qTable;
        private string filePath = "Assets/Scripts/GrupoH/TABLAQ_UNITY.csv";
        private WorldInfo _worldInfo;

        //inicializamos los parametrso y los metodos
        public void Initialize(WorldInfo worldInfo)
        {
            _worldInfo = worldInfo;
            _qTable = new Dictionary<(bool, bool, bool, bool, bool, bool, bool, bool), Dictionary<int, float>>();
            LoadQTable();
            Debug.Log("MyTester: initialized with Q-Table loaded.");
        }

        public CellInfo GetNextStep(CellInfo currentPosition, CellInfo otherPosition)
        {
            var state = GetState(currentPosition, otherPosition);

            var actions = new Dictionary<int, float>(_qTable[state]);
            int bestAction = SelectBestAction(actions);

            Directions direction = (Directions)bestAction;
            CellInfo nextPosition = _worldInfo.NextCell(currentPosition, direction);

            return nextPosition;
        }

        private (bool, bool, bool, bool, bool, bool, bool, bool) GetState(CellInfo currentPosition, CellInfo otherPosition)
        {
            var nextCellNorth = _worldInfo.NextCell(currentPosition, Directions.Up);
            var nextCellSouth = _worldInfo.NextCell(currentPosition, Directions.Down);
            var nextCellEast = _worldInfo.NextCell(currentPosition, Directions.Right);
            var nextCellWest = _worldInfo.NextCell(currentPosition, Directions.Left);

            bool canMoveNorth = nextCellNorth != null && nextCellNorth.Walkable;
            bool canMoveSouth = nextCellSouth != null && nextCellSouth.Walkable;
            bool canMoveEast = nextCellEast != null && nextCellEast.Walkable;
            bool canMoveWest = nextCellWest != null && nextCellWest.Walkable;

            bool fromNorth = otherPosition.y < currentPosition.y;
            bool fromSouth = otherPosition.y > currentPosition.y;
            bool fromEast = otherPosition.x > currentPosition.x;
            bool fromWest = otherPosition.x < currentPosition.x;

            return (canMoveNorth, canMoveSouth, canMoveEast, canMoveWest, fromNorth, fromSouth, fromEast, fromWest);
        }

        //Selecciona la acción que tengo mayor valor en Q
        private int SelectBestAction(Dictionary<int, float> actions)
        {
            return actions.Aggregate((max, current) => current.Value > max.Value ? current : max).Key;
        }

        //inicializamos estados con valor Q = 0
        private Dictionary<int, float> InitializeState()
        {
            var state = new Dictionary<int, float>();
            foreach (int action in new[] { (int)Directions.Up, (int)Directions.Down, (int)Directions.Right, (int)Directions.Left })
            {
                state[action] = 0.0f; // Inicializacion en 0
            }
            return state;
        }

        //buscamos y cargamos la tablaQ para iniciar la simulacion
        public void LoadQTable()
        {
            if (!File.Exists(filePath))
            {
                Debug.Log("No se encontro un archivo de tabla Q existente. Inicializando una nueva.");
                return;
            }

            using StreamReader reader = new StreamReader(filePath);
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(';');

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
                    _qTable[state] = InitializeState();
                }

                _qTable[state][actionUp] = qUp;
                _qTable[state][actionDown] = qDown;
                _qTable[state][actionRight] = qRight;
                _qTable[state][actionLeft] = qLeft;
            }
        }
    }
}
