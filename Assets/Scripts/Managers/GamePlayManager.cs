using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;
using System;
using System.Collections;
using UnityEngine.SceneManagement;
public enum GameState
{
    GenerateMap,
    Lose,
    Pause,
    SelectBall,
    SelectNodeToPlaceBall,
    GetballToSpawn,
    SpawnBall
}
public class GamePlayManager : MonoBehaviour
{
    public static event Action<int> OnLosingGame;
    public static event Action<int> OnUpdateScoreGame;
    public static event Action<GameState> OnPauseGame;

    LineRenderer line;
    public static GamePlayManager instance;
    [Header("Grid Generation")]
    [SerializeField] int rows = 9;
    [SerializeField] int columns = 9;
    [SerializeField] float spawnTime = 0.5f;
    public Color firstColor;
    public Color secondColor;
    public List<Node> nodeList;
    List<Ball> ballList;
    List<Ball> ballToSpawnList;
    List<Node> checkNodeList;
    [Header("Prefabs")]
    [SerializeField] Node nodePrefab;
    public GameState currentState;
    public Node currentSelectedNode;
    public Node[,] gridArray;
    PathFinder pathFinder;
    List<Node> path = new List<Node>();
    [SerializeField] int ballToSpawn = 3;
    [SerializeField] bool needReset;
    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 0;
        pathFinder = GetComponent<PathFinder>();
        if (instance == null)
        {
            instance = this;
        }
        gridArray = new Node[columns, rows];
        if(needReset)
        {
            if(PlayerPrefs.HasKey("bestScore"))
            {
                PlayerPrefs.DeleteKey("bestScore");
            }
        }
    }
    void Start()
    {
        SwitchState(GameState.GenerateMap);

    }
    public bool FindPath(int _startX,int _startY, int _endX, int _endY)
    {
        pathFinder.SetDistance(_startX, _startY);
        path = pathFinder.SetPath(_endX, _endY);
        if (path != null)
        {
            line.enabled = true;
            line.positionCount = path.Count;
            int lineIndex = 0;
            while (path.Count > 0)
            {
                Node firstNode = path.First();
                line.SetPosition(lineIndex, firstNode.transform.position);
                path.RemoveAt(0);
                lineIndex++;
                Invoke("DisableLine", 0.5f);
            }
            return true;
        }
        else
        {
            return false;
        }
    }
    void DisableLine()
    {
        line.enabled = false;
    }
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            PauseGameManager();
        }
        if(Input.GetKeyDown(KeyCode.L))
        {
             SetLose();
        }
    }

    void GenerateNewGrid(int _rows, int _columns)
    {
        nodeList = new List<Node>();
        ballList = new List<Ball>();
        checkNodeList = new List<Node>();
        ballToSpawnList = new List<Ball>(); 
        Camera.main.transform.position = new Vector3((_rows / 2) + 0.5f, (_columns / 2) + 1f, -10);
        
        for (int x = 0; x < _columns; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                var newNode = Instantiate(nodePrefab, new Vector2(x, y), Quaternion.identity);
                nodeList.Add(newNode);
                newNode.transform.parent = transform;
                newNode.GetComponent<SpriteRenderer>().color = ((x + y) % 2 == 0) ? firstColor : secondColor;
                gridArray[x, y] = newNode;
            }
        }
        Debug.Log(nodeList.Count);
    }
    void GetBallToSpawn(int _ballAmountToSpawn)
    {
        var freeNodes = nodeList.Where(node => node.occupiedBall == null).OrderBy(b => UnityEngine.Random.value).ToList();
        foreach (var freeNode in freeNodes.Take(_ballAmountToSpawn))
        {   
            float spawnRate = UnityEngine.Random.Range(0f, 1f);
            Ball newBall;
            newBall = SelectBallToSpawn(spawnRate);
            newBall.gameObject.SetActive(true);
            newBall.transform.position = freeNode.transform.position;
            newBall.transform.localScale = Vector2.zero;
            newBall.transform.DOScale (new Vector2(0.3f,0.3f),0.1f);
            newBall.GetComponentInChildren<SpriteRenderer>().sortingOrder = 2;
            ballToSpawnList.Add(newBall);
            SwitchState(GameState.SelectBall);
        }
        

    }

    IEnumerator SpawnBalls(int _ballAmountToSpawn)
    {
        
        yield return new WaitForSeconds(spawnTime);
       
        foreach (var ball in ballToSpawnList.Take(_ballAmountToSpawn))
        {
            Node nodeToCheck = GetNodebyPosition(ball.transform.position);
            if (nodeToCheck.occupiedBall == null)
            {
                Spawn(nodeToCheck.transform.position, ball);

            }
            else

            {
                var freeNodes = nodeList.Where(node => node.occupiedBall == null).OrderBy(b => UnityEngine.Random.value).ToList();
                Spawn(freeNodes.First().transform.position, ball);
            }
        }
        ballToSpawnList.Clear();
        SwitchState(GameState.GetballToSpawn);
        CheckLoseCondition();
    }
   
    void Spawn(Vector2 _spawnPos,Ball _ballToSpawn)
    {
        _ballToSpawn.GetComponentInChildren<SpriteRenderer>().sortingOrder = 3;
        _ballToSpawn.OnSpawnAnimation();
        _ballToSpawn.transform.position = _spawnPos;
        ballList.Add(_ballToSpawn);
        _ballToSpawn.transform.parent = transform;
        
        Node occupiedNode = nodeList.Where(node => (Vector2)node.transform.position == _spawnPos).First();
        occupiedNode.occupiedBall = _ballToSpawn;
        if (CheckExplode(occupiedNode))
        {
            Debug.Log("Explode");
            ExplodeBalls();
        }

    }

    private static Ball SelectBallToSpawn(float spawnRate)
    {
        Ball newBall;
        if (spawnRate < 0.80f)
        {
            newBall = PoolingManager.instance.ballQueue.Dequeue();
        }
        else if (spawnRate < .95f)
        {
            newBall = PoolingManager.instance.ghostBallQueue.Dequeue();
        }
        else
        {

            newBall = PoolingManager.instance.rainbowBallQueue.Dequeue();
        }

        return newBall;
    }

    public void SelectBall(Node _selectedNode)
    {
        DeSelectNode();
        _selectedNode.selectedballSprite.SetActive(true);
        _selectedNode.isSelected = true;
        currentSelectedNode = _selectedNode;
        currentSelectedNode.occupiedBall.ballAni.SetBool("isSelected", true);
        SwitchState(GameState.SelectNodeToPlaceBall);
    }
    public void SelectNode(Node _newNode)
    {


        DeSelectNode();

        bool hasPath = FindPath((int)currentSelectedNode.transform.position.x, (int)currentSelectedNode.transform.position.y, (int)_newNode.transform.position.x, (int)_newNode.transform.position.y);
        if (currentSelectedNode.occupiedBall.type == BallType.Ghost)
        {
            MoveBall(currentSelectedNode, _newNode);
        }
        else if (hasPath)
        {
            MoveBall(currentSelectedNode, _newNode);
        }

    }
    void MoveBall(Node _currentNode,Node _newNode)
    {
        SoundManager.instance.PlaySound("BallMove");
        _newNode.occupiedBall = _currentNode.occupiedBall;
        _currentNode.occupiedBall = null;
        _newNode.occupiedBall.transform.position = _newNode.transform.position;
        if (CheckExplode(_newNode))
        {
            ExplodeBalls();
        }
        currentSelectedNode = _newNode;
        SwitchState(GameState.SpawnBall);
    }
    public void DeSelectNode()
    {
        foreach (var node in nodeList)
        {
            node.selectedballSprite.SetActive(false);
            node.isSelected = false;
            if (node.occupiedBall != null)
            {
                node.occupiedBall.ballAni.SetBool("isSelected", false);
            }

        }
    }
    public void SwitchState(GameState _newState)
    {
        currentState = _newState;
        switch (currentState)
        {
            case GameState.GenerateMap:
                GenerateNewGrid(rows, columns);
                GetBallToSpawn(20);
                StartCoroutine((SpawnBalls(20)));
                break;
            case GameState.Pause:
                break;
            case GameState.Lose:
                OnLosingGame?.Invoke(UIManager.instance.currentScore);
                break;
            case GameState.SelectBall:
                break;
            case GameState.SelectNodeToPlaceBall:
                break;
            case GameState.SpawnBall:
                StartCoroutine(SpawnBalls(ballToSpawn));
                break;
            case GameState.GetballToSpawn:
                GetBallToSpawn(ballToSpawn);
                UIManager.instance.DisplayBall(ballToSpawnList);

                break;
            default:
                break;
        }
    }
    bool CheckExplode(Node _checkNode)
    {
        int col, row, count = 0;
        Node nodeToCompare;
        bool breakLoop = false;
        col = (int)_checkNode.transform.position.x+1;
        Node tempNode = _checkNode;
        checkNodeList.Add(_checkNode);
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(col, _checkNode.transform.position.y));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.type == BallType.Rainbow)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x+1, _checkNode.transform.position.y));
                    }
                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);

                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }
            col++;
        }
        while (breakLoop);
        col = (int)_checkNode.transform.position.x - 1;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(col, _checkNode.transform.position.y));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.type == BallType.Rainbow && tempNode.occupiedBall.colorValue != GetNodebyPosition(new Vector2(_checkNode.transform.position.x - 1, _checkNode.transform.position.y)).occupiedBall.colorValue && count<4)
                    {
                        count = 0;
                        checkNodeList.Clear();
                        checkNodeList.Add(_checkNode);
                    }
                    if(count<4)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x - 1, _checkNode.transform.position.y));
                    }

                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);

                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }
            col--;
        }
        while (breakLoop);

        if (count > 3)
        {
            return true;
        }

        checkNodeList.Clear();
        count = 0;
        tempNode = _checkNode;
        checkNodeList.Add(_checkNode);
        row = (int)_checkNode.transform.position.y+1;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(_checkNode.transform.position.x, row));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.type == BallType.Rainbow)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x, _checkNode.transform.position.y+1));
                    }
                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);

                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }



            row++;
        }
        while (breakLoop);
        row = (int)_checkNode.transform.position.y - 1;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(_checkNode.transform.position.x, row));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.colorValue != GetNodebyPosition(new Vector2(_checkNode.transform.position.x , _checkNode.transform.position.y-1)).occupiedBall.colorValue && count < 4)
                    {
                        count = 0;
                        checkNodeList.Clear();
                        checkNodeList.Add(_checkNode);
                    }
                    if (count < 4)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x, _checkNode.transform.position.y-1));
                    }

                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);
                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }
            row--;
        } while (breakLoop);
        if (count > 3)
        {
            return true;
        }
        checkNodeList.Clear();
        col = (int)_checkNode.transform.position.x + 1;
        row = (int)_checkNode.transform.position.y + 1;
        tempNode = _checkNode;
        checkNodeList.Add(_checkNode);
        count = 0;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(col, row));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.type == BallType.Rainbow)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x + 1, _checkNode.transform.position.y + 1));
                    }
                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);

                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }


            col++;
            row++;
        }
        while (breakLoop);
        col = (int)_checkNode.transform.position.x - 1;
        row = (int)_checkNode.transform.position.y - 1;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(col, row));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {

                    if (tempNode.occupiedBall.colorValue != GetNodebyPosition(new Vector2(_checkNode.transform.position.x - 1, _checkNode.transform.position.y - 1)).occupiedBall.colorValue && count < 4)
                    {
                        count = 0;
                        checkNodeList.Clear();
                        checkNodeList.Add(_checkNode);
                    }
                    if (count < 4)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x - 1, _checkNode.transform.position.y - 1));
                    }

                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);
                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }
            col--;
            row--;
        } while (breakLoop);
        if (count > 3)
        {
            return true;
        }
        checkNodeList.Clear();
        tempNode = _checkNode;
        col = (int)_checkNode.transform.position.x - 1;
        row = (int)_checkNode.transform.position.y + 1;
        checkNodeList.Add(_checkNode);
        count = 0;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(col, row));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.type == BallType.Rainbow)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x - 1, _checkNode.transform.position.y + 1));
                    }
                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);

                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }


            col--;
            row++;
        }
        while (breakLoop);
        col = (int)_checkNode.transform.position.x + 1;
        row = (int)_checkNode.transform.position.y - 1;
        do
        {
            breakLoop = true;
            nodeToCompare = GetNodebyPosition(new Vector2(col, row));
            if (nodeToCompare != null)
            {
                if (nodeToCompare.occupiedBall != null)
                {
                    if (tempNode.occupiedBall.colorValue != GetNodebyPosition(new Vector2(_checkNode.transform.position.x + 1, _checkNode.transform.position.y - 1)).occupiedBall.colorValue && count < 4)
                    {
                        count = 0;
                        checkNodeList.Clear();
                        checkNodeList.Add(_checkNode);
                    }
                    if (count < 4)
                    {
                        tempNode = GetNodebyPosition(new Vector2(_checkNode.transform.position.x + 1, _checkNode.transform.position.y - 1));
                    }

                    if (nodeToCompare.occupiedBall.colorValue == tempNode.occupiedBall.colorValue || nodeToCompare.occupiedBall.type == BallType.Rainbow)
                    {
                        checkNodeList.Add(nodeToCompare);
                        count++;
                    }
                    else
                    {
                        breakLoop = false;
                    }
                }
                else
                {
                    breakLoop = false;
                }
            }
            else
            {
                breakLoop = false;
            }
            col++;
            row--;
        } while (breakLoop);
        if (count > 3)
        {
            return true;
        }
        checkNodeList.Clear();
        return false;
    }
    public Node GetNodebyPosition(Vector2 _position)
    {
        foreach (var node in nodeList)
        {
            if ((Vector2)node.transform.position == _position)
            {
                return node;
            }

        }
        return null;

    }
    void ExplodeBalls()
    {
        SoundManager.instance.PlaySound("BallExplode");
        Debug.Log(checkNodeList.Count);
        foreach (var checkNode in checkNodeList)
        {
            checkNode.occupiedBall.OnExplode();
            
            checkNode.occupiedBall.gameObject.SetActive(false);
            PoolingManager.instance.DeActiveBall(checkNode.occupiedBall);
            checkNode.occupiedBall = null;
        }
        UIManager.instance.UpdateAndDisplayScore(checkNodeList.Count);
        OnUpdateScoreGame?.Invoke(checkNodeList.Count);
        checkNodeList.Clear();

    }
    void CheckLoseCondition()
    {
        var freeNode = nodeList.Where(n => n.occupiedBall == null).ToList();
        if(freeNode.Count<4)
        {
            SwitchState(GameState.Lose);
        }    
    }
    public void PauseGameManager()
    {   
        GameState stateBeforePause= GameState.SelectBall;
        if(currentState != GameState.Pause)
        {
            stateBeforePause = currentState;
            SwitchState(GameState.Pause);
            Time.timeScale = 0;
        }
        else
        {
            Time.timeScale = 1;
            SwitchState(stateBeforePause);
        }
        OnPauseGame?.Invoke(currentState);
    }
    public void ReplayGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void ExitGame()
    {
        Application.Quit();
    }
    void SetLose()
    {
        SwitchState(GameState.Lose);
    }
}
