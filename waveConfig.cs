using System.Collections.Generic;
using UnityEngine;

public class waveConfig : MonoBehaviour
{
    [Header("Spawn Setup")]
    public List<GameObject> enemyPrefab = new();
    public List<int> totalToSpawn = new();
    public float intervalSeconds = 0.5f;
    public float spawnRadius = 0f;

    [Header("Scene")]
    public Transform startTransform;
    public Transform finalTarget;

    [Header("Grid Settings")]
    public bool autoFitArena = true;
    public Vector2 arenaSize = new Vector2(50f, 50f);
    public float cellSize = 1f;
    public float arenaPadding = 2f;

    [Header("Walls")]
    public string breakableTag = "Breakable";

    [Header("Outputs")]
    public List<GameObject> targets = new List<GameObject>();


    private class Node
    {
        public int x, y;
        public Vector3 worldPos;
        public GameObject wall;
        public float gCost = float.MaxValue;
        public float hCost;
        public Node parent;
        public float fCost => gCost + hCost;
    }


    private Node[,] grid;
    private int gridSizeX, gridSizeY;
    private Vector3 gridOrigin;
    private List<Node> pathNodes = new List<Node>();

    private void Start()
    {
        if (startTransform == null) startTransform = transform;

        BuildGrid();
        RunAStar();
    }


    private void BuildGrid()
    {
        if (cellSize <= 0.0001f) cellSize = 1f;

        if (autoFitArena)
        {
            AutoFitArenaBounds(out gridOrigin, out gridSizeX, out gridSizeY);
        }
        else
        {
            gridSizeX = Mathf.Max(1, Mathf.CeilToInt(arenaSize.x / cellSize));
            gridSizeY = Mathf.Max(1, Mathf.CeilToInt(arenaSize.y / cellSize));
            gridOrigin = transform.position - new Vector3(arenaSize.x, 0, arenaSize.y) * 0.5f;
        }

        grid = new Node[gridSizeX, gridSizeY];

        var walls = GameObject.FindGameObjectsWithTag(breakableTag);

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPos = IndexToWorld(x, y);
                var node = new Node { x = x, y = y, worldPos = worldPos, wall = null };

                foreach (var wall in walls)
                {
                    var col = wall.GetComponent<Collider>();
                    if (col && col.bounds.Contains(worldPos))
                    {
                        node.wall = wall;
                        break;
                    }
                }
                grid[x, y] = node;
            }
        }
    }

    private void AutoFitArenaBounds(out Vector3 origin, out int sizeX, out int sizeY)
    {
        float minX = Mathf.Min(startTransform.position.x, finalTarget.position.x);
        float maxX = Mathf.Max(startTransform.position.x, finalTarget.position.x);
        float minZ = Mathf.Min(startTransform.position.z, finalTarget.position.z);
        float maxZ = Mathf.Max(startTransform.position.z, finalTarget.position.z);

        var walls = GameObject.FindGameObjectsWithTag(breakableTag);

        foreach (var wall in walls)
        {
            var col = wall.GetComponent<Collider>();
            if (!col) continue;
            var b = col.bounds;
            minX = Mathf.Min(minX, b.min.x);
            maxX = Mathf.Max(maxX, b.max.x);
            minZ = Mathf.Min(minZ, b.min.z);
            maxZ = Mathf.Max(maxZ, b.max.z);

        }

        minX -= arenaPadding; maxX += arenaPadding;
        minZ -= arenaPadding; maxZ += arenaPadding;

        float width = Mathf.Max(1f, maxX - minX);
        float depth = Mathf.Max(1f, maxZ - minZ);

        sizeX = Mathf.Max(1, Mathf.CeilToInt(width / cellSize));
        sizeY = Mathf.Max(1, Mathf.CeilToInt(depth / cellSize));

        origin = new Vector3(minX, transform.position.y, minZ);
    }

    Vector3 IndexToWorld(int x, int y) //2d
    {
        return new Vector3(gridOrigin.x + (x + 0.5f) * cellSize, transform.position.y, gridOrigin.z + (y + 0.5f) * cellSize); //3d - > 2d
    }

    private void RunAStar()
    {
        Node startNode = WorldToNode(startTransform.position, clampToGrid: true);
        Node goalNode = WorldToNode(finalTarget.position, clampToGrid: true);

        if (startNode == null || goalNode == null) return;

        foreach(var n in grid)
        {
            n.gCost = float.MaxValue;
            n.hCost = 0f;
            n.parent = null;
        }

        var open = new List<Node>();
        var closed = new HashSet<Node>();

        startNode.gCost = 0f;
        startNode.hCost = Heuristic(startNode, goalNode);
        open.Add(startNode);

        while (open.Count > 0)
        {
            Node current = LowestF(open);
            open.Remove(current);
            closed.Add(current);

            if (current == goalNode) 
            {
                pathNodes = ReconstructPath(goalNode);
                ExtractTargets();
                return;
            }

            foreach(var neighbor in GetNeighbors(current))
            {
                if (closed.Contains(neighbor)) continue;

                float stepCost = 0f;
                if (neighbor.wall)
                {
                    var s = neighbor.wall.GetComponent<stats>();
                    stepCost = s ? Mathf.Max(0f, s.health) : 0f;
                }

                float tenativeG = current.gCost + stepCost;

                if (tenativeG < neighbor.gCost)
                {
                    neighbor.parent = current;
                    neighbor.gCost = tenativeG;
                    neighbor.hCost = Heuristic(neighbor, goalNode);
                    if(!open.Contains(neighbor)) open.Add(neighbor);
                }
            } 
        }
    }

    private IEnumerable<Node> GetNeighbors(Node node)
    {
        int[,] deltas = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        for (int i = 0; i < deltas.GetLength(0); i++)
        {
            int nx = node.x + deltas[i, 0];
            int ny = node.y + deltas[i, 1];

            if (nx >= 0 && ny >= 0 && nx < gridSizeX && ny < gridSizeY)
                yield return grid[nx, ny];
        }
    }

    private void ExtractTargets()
    {
        targets.Clear();

        HashSet<GameObject> seen = new HashSet<GameObject>();

        foreach (var n in pathNodes)
        {
            if (n.wall && seen.Add(n.wall)) targets.Add(n.wall);
        }

        targets.Add(finalTarget.gameObject);
    }

    private List<Node> ReconstructPath(Node goal)
    {
        var path = new List<Node>();
        Node c = goal;

        while (c != null)
        {
            path.Add(c);
            c = c.parent;
        }

        path.Reverse();
        return path;
    }

    private float Heuristic(Node a, Node b)
    {
        return Vector3.Distance(a.worldPos , b.worldPos) * 0.01f;
    }

    private Node LowestF(List<Node> list)
    {
        Node best = list[0];
        for(int i = 1; i < list.Count; i++)
            if (list[i].fCost < best.fCost) best = list[i];

        return best;
    }

    private Node WorldToNode(Vector3 worldPos, bool clampToGrid) 
    {
        float dx = (worldPos.x - gridOrigin.x) / cellSize;
        float dz = (worldPos.z - gridOrigin.z) / cellSize;

        int ix = Mathf.FloorToInt(dx);
        int iy = Mathf.FloorToInt(dz);

        if(ix < 0 ||  iy < 0 || ix >= gridSizeX || iy >= gridSizeY)
        {
            if(!clampToGrid) return null;

            ix = Mathf.Clamp(ix, 0, gridSizeX - 1);
            iy = Mathf.Clamp(iy, 0, gridSizeY - 1);
        } 

        return grid[ix,iy];
    }
}