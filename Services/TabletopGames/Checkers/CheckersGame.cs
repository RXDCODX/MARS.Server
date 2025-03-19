//using MARS.Server.Services.TabletopGames.Entitys;
//using Telegramus.Migrations;

//namespace MARS.Server.Services.TabletopGames.Checkers;

//public class CheckersGame
//{
//    private readonly GameBoard _gameBoard = GameBoard.CreateDefaultBoard();
//    public List<Cell[]> Logs = [];

//    public Cell? GetCell(string coordinates)
//    {
//        if (coordinates.Length != 2)
//        {
//            return null;
//        }

//        var result = ValidateAndGetCoordinates(coordinates);

//        if (result is not null)
//        {
//            foreach (var cell in _gameBoard.Board)
//            {
//                if (
//                    cell.YCoordinate == result.Value.YCoordinate
//                    && cell.XCoordinate == result.Value.XCoordinate
//                )
//                {
//                    return cell;
//                }
//            }
//        }

//        return null;
//    }

//    public (bool IsSuccess, bool CanContinue, string Message) TryToDoAMove(
//        Cell sourceCell,
//        Cell targetCell
//    )
//    {
//        // Проверка, что исходная клетка занята (там должна быть шашка)
//        if (!sourceCell.IsBusy)
//        {
//            return (false, false, "Исходная клетка пуста. Выберите клетку с шашкой.");
//        }

//        // Проверка, что целевая клетка пуста (для обычного хода или атаки)
//        if (!targetCell.IsBusy)
//        {
//            // Проверка, что ход выполняется по диагонали
//            if (IsValidMove(sourceCell, targetCell))
//            {
//                // Обычный ход
//                targetCell.IsBusy = true;
//                targetCell.IsKing = sourceCell.IsKing; // Сохраняем статус дамки
//                sourceCell.IsBusy = false;
//                sourceCell.IsKing = false;
//                return (true, false, "Ход выполнен.");
//            }
//            else
//            {
//                return (false, false, "Невозможно выполнить ход. Шашки ходят только по диагонали.");
//            }
//        }
//        else
//        {
//            // Проверка, возможна ли атака
//            var (isCanAttack, reason) = CanAttack(sourceCell, targetCell);

//            if (isCanAttack)
//            {
//                // Выполняем атаку
//                attackTarget.IsBusy = false; // Убираем вражескую шашку
//                attackTarget.IsKing = false;

//                // Перемещаем шашку на новую позицию
//                targetCell.IsBusy = true;
//                targetCell.IsKing = sourceCell.IsKing;
//                sourceCell.IsBusy = false;
//                sourceCell.IsKing = false;

//                // Проверка, может ли дамка продолжить атаку
//                bool canContinue = false;
//                if (targetCell.IsKing)
//                {
//                    canContinue = CanContinueAttack(targetCell);
//                }

//                return (
//                    true,
//                    canContinue,
//                    "Атака выполнена." + (canContinue ? " Можете продолжить атаку." : "")
//                );
//            }
//            else
//            {
//                return (
//                    false,
//                    false,
//                    "Невозможно выполнить атаку. Чтобы атаковать, выберите клетку за вражеской шашкой."
//                );
//            }
//        }
//    }

//    // Проверка, что ход выполняется по диагонали
//    private bool IsValidMove(Cell sourceCell, Cell targetCell)
//    {
//        // Проверка, что координаты целевой клетки находятся в пределах доски
//        if (
//            targetCell.XCoordinate < 'a'
//            || targetCell.XCoordinate > 'h'
//            || targetCell.YCoordinate < 1
//            || targetCell.YCoordinate > 8
//        )
//        {
//            return false; // Клетка за пределами доски
//        }

//        int xDiff = Math.Abs(targetCell.XCoordinate - sourceCell.XCoordinate);
//        int yDiff = Math.Abs(targetCell.YCoordinate - sourceCell.YCoordinate);

//        // Обычная шашка может ходить только на одну клетку
//        if (!sourceCell.IsKing)
//        {
//            // Проверка направления хода (вперед)
//            if (sourceCell.Color == Color.White && targetCell.YCoordinate <= sourceCell.YCoordinate)
//            {
//                return false; // Белая шашка не может ходить назад
//            }
//            if (sourceCell.Color == Color.Black && targetCell.YCoordinate >= sourceCell.YCoordinate)
//            {
//                return false; // Черная шашка не может ходить назад
//            }

//            return xDiff == 1 && yDiff == 1;
//        }
//        // Дамка может ходить на любое количество клеток по диагонали
//        else
//        {
//            return xDiff == yDiff;
//        }
//    }

//    // Проверка, возможна ли атака
//    private (bool isCanAttack, string reason) CanAttack(
//        Cell sourceCell,
//        Cell targetCell,
//        out Cell damagedCell
//    )
//    {
//        damagedCell = null; // Инициализация выходного параметра

//        if (targetCell.IsBusy)
//        {
//            return (false, "Выбрана неправильная клетка для атаки.");
//        }

//        if (!sourceCell.IsBusy)
//        {
//            return (false, "Клетка для передвижения пуста.");
//        }

//        var isKing = sourceCell.IsKing;

//        var (isDiagonal, cellsBetween) = CheckDiagonalAndDistance(sourceCell, targetCell);

//        if (!isDiagonal)
//        {
//            return (false, "Выбранные клетки не находятся на одной диагонали!");
//        }

//        if (!isKing && cellsBetween != 1)
//        {
//            return (false, "Обычная шашка может атаковать только через одну клетку.");
//        }

//        // Определяем направление движения
//        int xDirection = Math.Sign(targetCell.XCoordinate - sourceCell.XCoordinate);
//        int yDirection = Math.Sign(targetCell.YCoordinate - sourceCell.YCoordinate);

//        // Переменная для хранения клетки с вражеской шашкой
//        Cell enemyCell = null;
//        int enemyCount = 0;

//        // Проверяем все клетки между sourceCell и targetCell
//        for (int i = 1; i < cellsBetween; i++)
//        {
//            char currentX = (char)(sourceCell.XCoordinate + xDirection * i);
//            ushort currentY = (ushort)(sourceCell.YCoordinate + yDirection * i);

//            // Получаем клетку с доски
//            Cell currentCell = _gameBoard.Board[currentX - 'a', currentY - 1];

//            if (currentCell == null)
//            {
//                return (false, "Клетка за пределами доски.");
//            }

//            if (currentCell.IsBusy)
//            {
//                // Проверяем, что шашка принадлежит врагу
//                if (currentCell.Color == sourceCell.Color)
//                {
//                    return (false, "На пути находится своя шашка.");
//                }

//                enemyCount++;
//                enemyCell = currentCell;
//            }
//        }

//        // Для обычной шашки должна быть ровно одна вражеская шашка
//        if (!isKing && enemyCount != 1)
//        {
//            return (false, "Обычная шашка может атаковать только через одну вражескую шашку.");
//        }

//        // Для дамки должна быть хотя бы одна вражеская шашка
//        if (isKing && enemyCount < 1)
//        {
//            return (false, "Дамка должна атаковать через хотя бы одну вражескую шашку.");
//        }

//        damagedCell = enemyCell; // Клетка с вражеской шашкой
//        return (true, "Хороший ход! Так держать!");
//    }

//    public (bool IsDiagonal, int CellsBetween) CheckDiagonalAndDistance(
//        Cell sourceCell,
//        Cell targetCell
//    )
//    {
//        // Вычисляем разницу по X и Y
//        int xDiff = Math.Abs(targetCell.XCoordinate - sourceCell.XCoordinate);
//        int yDiff = Math.Abs(targetCell.YCoordinate - sourceCell.YCoordinate);

//        // Проверяем, что клетки находятся на одной диагонали
//        if (xDiff == yDiff)
//        {
//            // Количество клеток между sourceCell и targetCell
//            int cellsBetween = xDiff - 1;
//            return (true, cellsBetween);
//        }
//        else
//        {
//            // Клетки не на одной диагонали
//            return (false, 0);
//        }
//    }

//    // Проверка, может ли дамка продолжить атаку
//    private bool CanContinueAttack(Cell currentCell)
//    {
//        // Проверяем все четыре диагональных направления
//        for (int xDir = -1; xDir <= 1; xDir += 2)
//        {
//            for (int yDir = -1; yDir <= 1; yDir += 2)
//            {
//                char nextX = (char)(currentCell.XCoordinate + xDir * 2);
//                ushort nextY = (ushort)(currentCell.YCoordinate + yDir * 2);

//                Cell attackTarget = GetCell(
//                    (char)(currentCell.XCoordinate + xDir),
//                    (ushort)(currentCell.YCoordinate + yDir)
//                );
//                Cell nextTarget = GetCell(nextX, nextY);

//                if (attackTarget is { IsBusy: true } && nextTarget is { IsBusy: false })
//                {
//                    return true;
//                }
//            }
//        }

//        return false;
//    }

//    // Вспомогательный метод для получения клетки по координатам
//    private Cell GetCell(char x, ushort y)
//    {
//        // Здесь должна быть логика получения клетки из игрового поля
//        // Например, если у вас есть игровое поле в виде массива:
//        // return Board[x - 'a', y - 1];
//        throw new NotImplementedException(
//            "Реализуйте метод GetCell для получения клетки по координатам."
//        );
//    }

//    public static (char XCoordinate, ushort YCoordinate)? ValidateAndGetCoordinates(string input)
//    {
//        // Проверка длины строки
//        if (input.Length != 2)
//        {
//            return null;
//        }

//        var xCoordinate = '\0'; // Инициализация координаты X
//        ushort yCoordinate = 0; // Инициализация координаты Y

//        var hasLetter = false;
//        var hasDigit = false;

//        foreach (var c in input)
//        {
//            if (char.IsLetter(c) && (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
//            {
//                xCoordinate = char.ToLower(c); // Сохраняем букву в координату X
//                hasLetter = true;
//            }
//            else if (char.IsDigit(c))
//            {
//                yCoordinate = ushort.Parse(c.ToString()); // Сохраняем цифру в координату Y
//                hasDigit = true;
//            }
//            else
//            {
//                return null;
//            }
//        }

//        // Проверка, что строка содержит ровно одну букву и одну цифру
//        if (!hasLetter || !hasDigit)
//        {
//            return null;
//        }

//        return (xCoordinate, yCoordinate);
//    }
//}
