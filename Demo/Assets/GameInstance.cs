﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInstance : MonoBehaviour
{
    public SpriteRenderer[] walls;
    public Rigidbody2D ball;
    public Goal leftGoal;
    public Goal rightGoal;
    public Transform leftScoreSprite;
    public Transform rightScoreSprite;
    int left;
    int right;
    public Camera zoomCam;
    public EvolvedPlayer evolved;
    public Material graphMaterial;
    Transform graphRoot;
    public float horizBorder = 0;
    public float vertBorder = 0;
    public bool selected { get; private set; }
    bool filtered;
    // Start is called before the first frame update
    private void Awake()
    {
        foreach (var wall in walls)
        {
            if (Math.Abs(wall.transform.localPosition.x) > horizBorder)
                horizBorder = Math.Abs(wall.transform.localPosition.x);

            if (Math.Abs(wall.transform.localPosition.y) > vertBorder)
                vertBorder = Math.Abs(wall.transform.localPosition.y);
        }
    }

    void Start()
    {
        zoomCam = GetComponentInChildren<Camera>();
        zoomCam.orthographicSize =  Mathf.Abs(leftScoreSprite.position.y- rightScoreSprite.position.y) * 0.55f;
        zoomCam.enabled = false;
        zoomCam.depth = 2;
        zoomCam.eventMask = ~zoomCam.cullingMask;
        Reset();
        leftGoal.OnCollision += (go) =>
        {
            if (go == ball.gameObject)
            {
                right++;
                UpdateScoreImages();
                Reset(-1);
            }
        };

        rightGoal.OnCollision += (go) =>
        {
            if (go == ball.gameObject)
            {
                left++;
                UpdateScoreImages();
                Reset(1);
            }
        };
    }

    public void ToggleSelect()
    {
        selected = !selected;
    }

    private void OnMouseDown()
    {
        zoomCam.enabled = true;
    }

    private void OnMouseUp()
    {
        zoomCam.enabled = false;
    }


    private void Update()
    {

        Color wallColor = Color.white;
        if(selected)
        {
            wallColor = Color.blue;
        }

        if(filtered)
        {
            wallColor = wallColor * 0.5f;
        }

        foreach(SpriteRenderer render in walls)
        {
            render.color = wallColor;
        }
    }

    private void Reset(int direction = -1)
    {
        ball.position = transform.position;
        ball.velocity = Vector3.right * direction * 3f * transform.lossyScale.x;
    }

    internal void SetGraph(GameCreator.Graph graph)
    {
        if (graphRoot != null)
        {
            Destroy(graphRoot.gameObject);
        }

        graphRoot = new GameObject("GraphRoot").transform;
        graphRoot.parent = transform;
        graphRoot.localPosition = new Vector3(0,0,20);
        graphRoot.localScale = Vector3.one;
        float vertAmount = (vertBorder * 2) / (graph.layers.Count + 1);
        float vertStart = vertBorder- vertAmount ;
        Dictionary<uint, Vector3> nodePositions = new Dictionary<uint, Vector3>();
        for (int i = 0; i < graph.layers.Count; i++)
        {
            float horizAmount = (horizBorder * 2) / (graph.layers[i].Count + 1);
            float horizStart = horizAmount - horizBorder;

            for(int j = 0; j < graph.layers[i].Count; j++)
            {
                var node = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                nodePositions[graph.layers[i][j]] = new Vector3(horizStart + j * horizAmount, vertStart - i * vertAmount);
                node.parent = graphRoot;
                node.localPosition = nodePositions[graph.layers[i][j]];
                node.localScale = Vector3.one;
            }
        }
        foreach(var connection in graph.connections)
        {
            if (!nodePositions.ContainsKey(connection.Item1) || !nodePositions.ContainsKey(connection.Item2))
                continue;
            var connectionLine = new GameObject("connection").AddComponent<LineRenderer>();
            connectionLine.transform.parent = graphRoot;
            connectionLine.transform.localPosition = Vector3.zero;
            connectionLine.transform.localScale = Vector3.one;
            connectionLine.positionCount = (2);
            connectionLine.startWidth = 0.4f;
            connectionLine.endWidth = 0.4f;
            connectionLine.material = graphMaterial;
   
            // we want the lines to use local space and not world space
            connectionLine.useWorldSpace = false;
            connectionLine.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            connectionLine.receiveShadows = false;
            connectionLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            //connectionLine.material.color = Color.red;
            connectionLine.SetPositions(new Vector3[] { nodePositions[connection.Item1], nodePositions[connection.Item2] });


        }
    }

    void UpdateScoreImages()
    {
        Vector3 val = leftScoreSprite.transform.localScale;
        val.x = 0.25f * Mathf.Clamp(left, 0, 20);
        leftScoreSprite.transform.localScale = val;

        val = rightScoreSprite.transform.localScale;
        val.x = 0.5f * Mathf.Clamp(right, 0, 20);
        rightScoreSprite.transform.localScale = val;
    }

    internal void FullReset()
    {
        left = 0;
        right = 0;
        selected = false;
        UpdateScoreImages();
        Reset();
        GetComponentInChildren<EvolvedPlayer>().Reset();
        GetComponentInChildren<EnemyAIController>().Reset();

    }
}
