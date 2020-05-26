﻿using SharpNeat.Phenomes;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameInstance : AbstractGameInstance
{

    public Rigidbody2D ball;
    public Goal leftGoal;
    public Goal rightGoal;
    public Transform leftScoreSprite;
    public Transform rightScoreSprite;
    int leftScore;
    int rightScore;

    public EvolvedPlayer evolved;

    public override int InputCount { get { return 6; } }
    public override int OutputCount { get { return 2; } }
    protected override void Start()
    {
        base.Start();
        Reset();
        leftGoal.OnCollision += (go) =>
        {
            if (go == ball.gameObject)
            {
                rightScore++;
                UpdateScoreImages();
                Reset(-1);
            }
        };

        rightGoal.OnCollision += (go) =>
        {
            if (go == ball.gameObject)
            {
                leftScore++;
                UpdateScoreImages();
                Reset(1);
            }
        };
    }

    public override void SetEvolvedBrain(IBlackBox blackBox, SharpNeat.Genomes.Neat.NeatGenome birthGeneration)
    {
        evolved.SetBrain(blackBox);
    }

    private void Reset(int direction = -1)
    {
        ball.position = transform.position;
        ball.velocity = Vector3.right * direction * 3f * transform.lossyScale.x;
    }

    protected override string GetInputLabel(int index)
    {
        switch(index)
        {
            case 0:
                return "Bias";
            case 1:
                return "Ball X Direction";
            case 2:
                return "Ball Y Direction";
            case 3:
                return "Ball X Velocity";
            case 4:
                return "Ball Y Velocity";
            case 5:
                return "Enemy X Direction";
            case 6:
                return "Enemy Y Direction";
        }
        return "Unknown";
    }

    protected override string GetOutputLabel(int index)
    {
        switch (index)
        {
            case 0:
                return "X Direction";
            case 1:
                return "Y Direction";
        }
        return "Unknown";
    }    

    void UpdateScoreImages()
    {
        Vector3 val = leftScoreSprite.transform.localScale;
        val.x = 0.25f * Mathf.Clamp(leftScore, 0, 20);
        leftScoreSprite.transform.localScale = val;

        val = rightScoreSprite.transform.localScale;
        val.x = 0.5f * Mathf.Clamp(rightScore, 0, 20);
        rightScoreSprite.transform.localScale = val;
    }

    public override void FullReset()
    {
        leftScore = 0;
        rightScore = 0;
        selected = false;
        UpdateScoreImages();
        Reset();
        GetComponentInChildren<EvolvedPlayer>().Reset();
        GetComponentInChildren<EnemyAIController>().Reset();

    }

    public override float CalculateFitness()
    {
        return (2*leftScore) - rightScore;
    }
}
