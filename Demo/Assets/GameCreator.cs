﻿using Redzen.Numerics;
using Redzen.Numerics.Distributions;
using Redzen.Random;
using Redzen.Sorting;
using SharpNeat;
using SharpNeat.Core;
using SharpNeat.Decoders;
using SharpNeat.Decoders.Neat;
using SharpNeat.DistanceMetrics;
using SharpNeat.Domains;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Genomes.Neat;
using SharpNeat.Network;
using SharpNeat.Phenomes;
using SharpNeat.SpeciationStrategies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameCreator : MonoBehaviour
{
    #region sharpNEAT variables
    ISpeciationStrategy<NeatGenome> _speciationStrategy;
    IList<Specie<NeatGenome>> _specieList;

    int _bestSpecieIdx;
    IRandomSource _rng = RandomDefaults.CreateRandomSource();

    NeatEvolutionAlgorithmParameters _eaParams;
    NeatEvolutionAlgorithmParameters _eaParamsComplexifying;
    NeatEvolutionAlgorithmParameters _eaParamsSimplifying;


    ComplexityRegulationMode _complexityRegulationMode;
    IComplexityRegulationStrategy _complexityRegulationStrategy;


    NeatAlgorithmStats _stats;
    protected NeatGenome _currentBestGenome;

    #endregion

    public Text proTipText;
    public Text generationLabel;

    public Toggle automaticModeSwitch;
    public Button nextGenerationButton;
    public Button resetButton;


    public Toggle highSpeedMode;
    public Toggle pauseEvolution;

    public GameObject automaticPanel;
    public GameObject interactivePanel;

    public int gamesToCreate  = 1;
    public AbstractGameInstance gamePrefab;
    int gameSq = 1;

    IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder;
    IGenomeFactory<NeatGenome> genomeFactory;
    List<NeatGenome> genomeList;
    List<AbstractGameInstance> games;
    public Camera mainCam;
    public bool interactiveMode = false;
    int currentTextIndex = 0;
    int textChangeSeconds = 5;
    float textChangeTimer = 0;
    uint generation = 0;

    public UnityEngine.Events.UnityEvent OnNewGeneration = new UnityEngine.Events.UnityEvent();

    string[] proTips = { "The background graphs show the neural networks used by the agents",
        "Left click on a game to zoom in and view details about the neural network",
        "The left player is controlled by a neural net, the right player is a human authored agent"};

    int automaticGenerationSeconds = 10;
    float generationTimer = 0;
    private void Awake()
    {

        _speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(new ManhattanDistanceMetric());

        _complexityRegulationMode = ComplexityRegulationMode.Complexifying;
        _complexityRegulationStrategy = new DefaultComplexityRegulationStrategy(ComplexityCeilingType.Relative,5);
        _eaParams = new NeatEvolutionAlgorithmParameters();

        _eaParamsSimplifying = _eaParams.CreateSimplifyingParameters();
        
        _eaParamsComplexifying = _eaParams;

        _stats = new NeatAlgorithmStats(_eaParams);


        proTipText.text = proTips[0];
        automaticPanel.SetActive(!interactiveMode);
        interactivePanel.SetActive(interactiveMode);

        nextGenerationButton.onClick.AddListener(() => { Debug.Log("NEW GENERATION"); NewGeneration(); });
        resetButton.onClick.AddListener(() => { InitialisePopulation(); });

        automaticModeSwitch.onValueChanged.AddListener((newValue) => {
            automaticPanel.SetActive(!newValue);
            interactivePanel.SetActive(newValue);
            interactiveMode = newValue;
            generationTimer = 0;
            paused = false;
        });
        

        NeatGenomeParameters _neatGenomeParams = new NeatGenomeParameters();
        NetworkActivationScheme _activationScheme = NetworkActivationScheme.CreateAcyclicScheme();
        _neatGenomeParams.FeedforwardOnly = _activationScheme.AcyclicNetwork;
        _neatGenomeParams.ActivationFn = SharpNeat.Network.LeakyReLU.__DefaultInstance;// LeakyReLU.__DefaultInstance;
        //_neatGenomeParams.ConnectionWeightMutationProbability = .3;
        _neatGenomeParams.AddNodeMutationProbability = .2;
        _neatGenomeParams.AddConnectionMutationProbability = .4;

        //_neatGenomeParams.DeleteConnectionMutationProbability = .1;
        genomeFactory = new NeatGenomeFactory(gamePrefab.InputCount, gamePrefab.OutputCount, _neatGenomeParams);
        

        genomeDecoder = new NeatGenomeDecoder(_activationScheme);
    }

    Graph GenerateGraph(NeatGenome genome)
    {
        List<List<uint>> layers = new List<List<uint>>();
        List<uint> outputLayer = new List<uint>();
        layers.Add(new List<uint>());
        List<uint> processedNodes = new List<uint>();
        List<System.Tuple<uint, uint>> connections = new List<System.Tuple<uint, uint>>();
        int i = 0;
        while (processedNodes.Count < genome.NodeList.Count)
        {
            var node = genome.NodeList[i++%genome.NodeList.Count] as NeuronGene;
            if (processedNodes.Contains(node.Id))
                continue;

            if (node.NodeType == NodeType.Bias ){
                layers[0].Add(node.Id);

                processedNodes.Add(node.Id);
                continue;
            }

           if(node.NodeType == NodeType.Input)
            {
                processedNodes.Add(node.Id);
                layers[0].Add(node.Id);
                continue;
            }

            if (node.NodeType == NodeType.Output)
            {
                foreach (var nodeId in node.SourceNeurons)
                {
                    connections.Add(new System.Tuple<uint, uint>(nodeId, node.Id));
                }
                processedNodes.Add(node.Id);
                outputLayer.Add(node.Id);
                continue;
            }

            int highestSourceLayer = 0;
            bool shouldSkip = false;
            foreach(var nodeId in node.SourceNeurons)
            {
                if(!processedNodes.Contains(nodeId))
                {
                    shouldSkip = true;
                    break;
                }
                for(int j = 0; j < layers.Count; j++)
                {
                    if(layers[j].Contains(nodeId))
                    {
                        if (j > highestSourceLayer)
                            highestSourceLayer = j;
                    }
                }
            }
            foreach (var nodeId in node.SourceNeurons)
            {
                connections.Add(new System.Tuple<uint, uint>(nodeId, node.Id));
            }
            if (shouldSkip)
            {
                continue;
            }
            if(highestSourceLayer+1 >= layers.Count)
            {
                layers.Add(new List<uint>());
            }
            layers[highestSourceLayer + 1].Add(node.Id);
            processedNodes.Add(node.Id);
        }
        layers.Add(outputLayer);
        return new Graph() { layers = layers, connections = connections };
    }

    // Start is called before the first frame update
    void Start()
    {
        games = new List<AbstractGameInstance>();
        bool done = false;
        while(!done)
        {
            if(gameSq * gameSq < gamesToCreate)
            {
                gameSq++;
            }
            else
            {
                done = true;
            }
        }
        var min  = mainCam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        var max= mainCam.ViewportToWorldPoint(new Vector3(1, 1, 0));
        var xAdj = (max.x - min.x) / gameSq;
        var yAdj = (max.y - min.y) / gameSq;
        Vector3 offset = new Vector3(((gameSq - 1)/2.0f) * -xAdj, ((gameSq - 1)/2.0f) * -yAdj);
        for (int i = 0; i < gamesToCreate; i++)
        {
            var x = i % gameSq;
            var y = (gamesToCreate - (i + 1)) / gameSq;
            var instance = GameObject.Instantiate<AbstractGameInstance>(gamePrefab);
            instance.transform.localScale = new Vector3(100.0f / gameSq, 100.0f / gameSq, 1);
            instance.transform.position = new Vector3(xAdj * x, yAdj*y) + offset;
            games.Add(instance);
        }
        InitialisePopulation();
    }

    void InitialisePopulation()
    {
        if(update != null)
            StopCoroutine(update);
        generationTimer = 0;
        generation = 0;
        paused = false;
        genomeList = genomeFactory.CreateGenomeList(gamesToCreate, 0);
        for (int i = 0; i < gamesToCreate; i++)
        {
            SetupGame(i);
        }
        OnNewGeneration.Invoke();
    }

    public void NewGeneration()
    {
        
        generationTimer = 0;
        paused = false;
        List<NeatGenome> selected = new List<NeatGenome>();
        for(int i = 0; i < genomeList.Count; i++)
        {
            if (games[i].selected)
                selected.Add(genomeList[i]);
        }

         if(selected.Count == 0)
        {
            return;
        }
        generation++;
        genomeList.Clear();
        genomeList.AddRange(selected);
        while(genomeList.Count < gamesToCreate)
        {
            int a  = Random.Range(0, selected.Count);
            int b = Random.Range(0, selected.Count);
            if(a==b)
            {
                genomeList.Add(selected[a].CreateOffspring(generation));                
            }
            else
            {
                genomeList.Add(selected[a].CreateOffspring(selected[b],generation));
            }
        }
        for (int i = 0; i < gamesToCreate; i++)
        {
            SetupGame(i);
        }

    }

    private void SetupGame(int i)
    {
        games[i].SetEvolvedBrain(genomeDecoder.Decode(genomeList[i]));
        games[i].SetGraph(GenerateGraph(genomeList[i]));
        games[i].FullReset();
    }

    void HandleInteractiveUpdate()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("Clickerd");
            var mousePos = Input.mousePosition;
            var ray = mainCam.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit raycastHit))
            {
                Debug.Log("Hit");

                var instance = raycastHit.collider.GetComponent<GameInstance>();
                if (instance != null)
                {
                    instance.ToggleSelect();
                }
            }
        }
    }

    bool paused = false;

    IEnumerator AnimateAutomaticSelectionProcess(List<int> gamesToBeSelected)
    {
        foreach(int gameIndex in gamesToBeSelected)
        {
            games[gameIndex].ToggleSelect();

            yield return new WaitForSeconds(0.5f);
            if (interactiveMode)
            {
                yield break;
            }
        }
        yield return new WaitForSeconds(2);
        if(interactiveMode)
        {
            yield break;
        }
        NewGeneration();
    }

    int[] CreateRandomIndexOrder(int count)
    {
        int[] ts = new int[count];
        for(int i= 0; i< count; i++)
        {
            ts[i] = i;
        }
        var last = count - 1;
        for (var i = 0; i < last; ++i)
        {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = ts[i];
            ts[i] = ts[r];
            ts[r] = tmp;
        }
        return ts;
    }

    bool emptySpeciesFlag;
    List<NeatGenome> offspringList;
    void HandleAutomaticUpdate()
    {
        Time.timeScale = highSpeedMode.isOn ? 3 : 1;
        foreach(var game in games)
        {
            if(game.interesting != game.selected)
            {
                game.ToggleSelect();
            }
        }
        if (paused || pauseEvolution.isOn)
        {
            return;
        }
        generationTimer += Time.deltaTime;

        if (generationTimer <= automaticGenerationSeconds)
        {
            return;
        }
        //paused = true;
        
        Dictionary<int, float> fitnesses = new Dictionary<int, float>();
        //Calculate the fitnesses of each game instance
        for (int i = 0; i < games.Count; i++)
        {
            AbstractGameInstance gi = games[i];
            genomeList[i].EvaluationInfo.SetFitness(1000+gi.CalculateFitness());
        }

        if (generation > 0)
        {

            //TODO: EVALUATE USED TO BE HERE
            // Integrate offspring into species.
            if (emptySpeciesFlag)
            {
                // We have one or more terminated species. Therefore we need to fully re-speciate all genomes to divide them
                // evenly between the required number of species.

                // Clear all genomes from species (we still have the elite genomes in _genomeList).
                ClearAllSpecies();

                // Speciate genomeList.
                _speciationStrategy.SpeciateGenomes(genomeList, _specieList);
            }
            else
            {
                // Integrate offspring into the existing species. 
                _speciationStrategy.SpeciateOffspring(offspringList, _specieList);
            }
            Debug.Assert(!TestForEmptySpecies(_specieList), "Speciation resulted in one or more empty species.");

            // Sort the genomes in each specie. Fittest first (secondary sort - youngest first).
            SortSpecieGenomes();

            // Update stats and store reference to best genome.
            UpdateBestGenome();
            UpdateStats();

            // Determine the complexity regulation mode and switch over to the appropriate set of evolution
            // algorithm parameters. Also notify the genome factory to allow it to modify how it creates genomes
            // (e.g. reduce or disable additive mutations).
            _complexityRegulationMode = _complexityRegulationStrategy.DetermineMode(_stats);
            genomeFactory.SearchMode = (int)_complexityRegulationMode;
            switch (_complexityRegulationMode)
            {
                case ComplexityRegulationMode.Complexifying:
                    _eaParams = _eaParamsComplexifying;
                    break;
                case ComplexityRegulationMode.Simplifying:
                    _eaParams = _eaParamsSimplifying;
                    break;
            }

            // TODO: More checks.
            Debug.Assert(genomeList.Count == gamesToCreate);
        }
        _specieList = _speciationStrategy.InitializeSpeciation(genomeList, _eaParams.SpecieCount);
        SortSpecieGenomes();
        UpdateBestGenome();
        // Calculate statistics for each specie (mean fitness, target size, number of offspring to produce etc.)
        int offspringCount;
        SpecieStats[] specieStatsArr = CalcSpecieStats(out offspringCount);

        // Create offspring.
        offspringList = CreateOffspring(specieStatsArr, offspringCount);

        // Trim species back to their elite genomes.
        emptySpeciesFlag = TrimSpeciesBackToElite(specieStatsArr);

        // Rebuild _genomeList. It will now contain just the elite genomes.
        RebuildGenomeList();

        // Append offspring genomes to the elite genomes in _genomeList. We do this before calling the
        // _genomeListEvaluator.Evaluate because some evaluation schemes re-evaluate the elite genomes 
        // (otherwise we could just evaluate offspringList).
        genomeList.AddRange(offspringList);


        for (int i = 0; i < gamesToCreate; i++)
        {
            SetupGame(i);
        }
        OnNewGeneration.Invoke();
        generation++;
        generationTimer = 0;
    }
    Coroutine update = null;

    public ulong EvaluationCount => throw new System.NotImplementedException();

    public bool StopConditionSatisfied => throw new System.NotImplementedException();

    // Update is called once per frame
    void Update()
    {
        generationLabel.text = "Generation: " + generation;
        proTipText.color = new Color(1,1,1,0.8f+0.2f*    Mathf.Sin(3*Time.unscaledTime));
        textChangeTimer += Time.unscaledDeltaTime;
        if(textChangeTimer > textChangeSeconds)
        {
            currentTextIndex = (currentTextIndex + 1) % proTips.Length;
            proTipText.text = proTips[currentTextIndex];
            textChangeTimer -= textChangeSeconds;
        }


        if (Input.GetKeyDown(KeyCode.R))
        {
            InitialisePopulation();
        }
        if (interactiveMode)
        {
            HandleInteractiveUpdate();
        }
        else
        {
            HandleAutomaticUpdate();
        }


    }


    #region Private Methods [High Level Algorithm Methods. CalcSpecieStats/CreateOffspring]

    /// <summary>
    /// Calculate statistics for each specie. This method is at the heart of the evolutionary algorithm,
    /// the key things that are achieved in this method are - for each specie we calculate:
    ///  1) The target size based on fitness of the specie's member genomes.
    ///  2) The elite size based on the current size. Potentially this could be higher than the target 
    ///     size, so a target size is taken to be a hard limit.
    ///  3) Following (1) and (2) we can calculate the total number offspring that need to be generated 
    ///     for the current generation.
    /// </summary>
    private SpecieStats[] CalcSpecieStats(out int offspringCount)
    {
        double totalMeanFitness = 0.0;

        // Build stats array and get the mean fitness of each specie.
        int specieCount = _specieList.Count;
        SpecieStats[] specieStatsArr = new SpecieStats[specieCount];
        for (int i = 0; i < specieCount; i++)
        {
            SpecieStats inst = new SpecieStats();
            specieStatsArr[i] = inst;
            inst._meanFitness = _specieList[i].CalcMeanFitness();
            totalMeanFitness += inst._meanFitness;
        }

        // Calculate the new target size of each specie using fitness sharing. 
        // Keep a total of all allocated target sizes, typically this will vary slightly from the
        // overall target population size due to rounding of each real/fractional target size.
        int totalTargetSizeInt = 0;

        if (0.0 == totalMeanFitness)
        {   // Handle specific case where all genomes/species have a zero fitness. 
            // Assign all species an equal targetSize.
            double targetSizeReal = (double)gamesToCreate / (double)specieCount;

            for (int i = 0; i < specieCount; i++)
            {
                SpecieStats inst = specieStatsArr[i];
                inst._targetSizeReal = targetSizeReal;

                // Stochastic rounding will result in equal allocation if targetSizeReal is a whole
                // number, otherwise it will help to distribute allocations evenly.
                inst._targetSizeInt = (int)NumericsUtils.ProbabilisticRound(targetSizeReal, _rng);

                // Total up discretized target sizes.
                totalTargetSizeInt += inst._targetSizeInt;
            }
        }
        else
        {
            // The size of each specie is based on its fitness relative to the other species.
            for (int i = 0; i < specieCount; i++)
            {
                SpecieStats inst = specieStatsArr[i];
                inst._targetSizeReal = (inst._meanFitness / totalMeanFitness) * (double)gamesToCreate;

                // Discretize targetSize (stochastic rounding).
                inst._targetSizeInt = (int)NumericsUtils.ProbabilisticRound(inst._targetSizeReal, _rng);

                // Total up discretized target sizes.
                totalTargetSizeInt += inst._targetSizeInt;
            }
        }

        // Discretized target sizes may total up to a value that is not equal to the required overall population
        // size. Here we check this and if there is a difference then we adjust the specie's targetSizeInt values
        // to compensate for the difference.
        //
        // E.g. If we are short of the required populationSize then we add the required additional allocation to
        // selected species based on the difference between each specie's targetSizeReal and targetSizeInt values.
        // What we're effectively doing here is assigning the additional required target allocation to species based
        // on their real target size in relation to their actual (integer) target size.
        // Those species that have an actual allocation below there real allocation (the difference will often 
        // be a fractional amount) will be assigned extra allocation probabilistically, where the probability is
        // based on the differences between real and actual target values.
        //
        // Where the actual target allocation is higher than the required target (due to rounding up), we use the same
        // method but we adjust specie target sizes down rather than up.
        int targetSizeDeltaInt = totalTargetSizeInt - gamesToCreate;

        if (targetSizeDeltaInt < 0)
        {
            // Check for special case. If we are short by just 1 then increment targetSizeInt for the specie containing
            // the best genome. We always ensure that this specie has a minimum target size of 1 with a final test (below),
            // by incrementing here we avoid the probabilistic allocation below followed by a further correction if
            // the champ specie ended up with a zero target size.
            if (-1 == targetSizeDeltaInt)
            {
                specieStatsArr[_bestSpecieIdx]._targetSizeInt++;
            }
            else
            {
                // We are short of the required populationSize. Add the required additional allocations.
                // Determine each specie's relative probability of receiving additional allocation.
                double[] probabilities = new double[specieCount];
                for (int i = 0; i < specieCount; i++)
                {
                    SpecieStats inst = specieStatsArr[i];
                    probabilities[i] = System.Math.Max(0.0, inst._targetSizeReal - (double)inst._targetSizeInt);
                }

                // Use a built in class for choosing an item based on a list of relative probabilities.
                DiscreteDistribution dist = new DiscreteDistribution(probabilities);

                // Probabilistically assign the required number of additional allocations.
                // FIXME/ENHANCEMENT: We can improve the allocation fairness by updating the DiscreteDistribution 
                // after each allocation (to reflect that allocation).
                // targetSizeDeltaInt is negative, so flip the sign for code clarity.
                targetSizeDeltaInt *= -1;
                for (int i = 0; i < targetSizeDeltaInt; i++)
                {
                    int specieIdx = DiscreteDistribution.Sample(_rng, dist);
                    specieStatsArr[specieIdx]._targetSizeInt++;
                }
            }
        }
        else if (targetSizeDeltaInt > 0)
        {
            // We have overshot the required populationSize. Adjust target sizes down to compensate.
            // Determine each specie's relative probability of target size downward adjustment.
            double[] probabilities = new double[specieCount];
            for (int i = 0; i < specieCount; i++)
            {
                SpecieStats inst = specieStatsArr[i];
                probabilities[i] = System.Math.Max(0.0, (double)inst._targetSizeInt - inst._targetSizeReal);
            }

            // Use a built in class for choosing an item based on a list of relative probabilities.
            DiscreteDistribution dist = new DiscreteDistribution(probabilities);

            // Probabilistically decrement specie target sizes.
            // ENHANCEMENT: We can improve the selection fairness by updating the DiscreteDistribution 
            // after each decrement (to reflect that decrement).
            for (int i = 0; i < targetSizeDeltaInt;)
            {
                int specieIdx = DiscreteDistribution.Sample(_rng, dist);

                // Skip empty species. This can happen because the same species can be selected more than once.
                if (0 != specieStatsArr[specieIdx]._targetSizeInt)
                {
                    specieStatsArr[specieIdx]._targetSizeInt--;
                    i++;
                }
            }
        }

        // We now have Sum(_targetSizeInt) == _populationSize. 
        Debug.Assert(SumTargetSizeInt(specieStatsArr) == gamesToCreate);

        // TODO: Better way of ensuring champ species has non-zero target size?
        // However we need to check that the specie with the best genome has a non-zero targetSizeInt in order
        // to ensure that the best genome is preserved. A zero size may have been allocated in some pathological cases.
        if (0 == specieStatsArr[_bestSpecieIdx]._targetSizeInt)
        {
            specieStatsArr[_bestSpecieIdx]._targetSizeInt++;

            // Adjust down the target size of one of the other species to compensate.
            // Pick a specie at random (but not the champ specie). Note that this may result in a specie with a zero 
            // target size, this is OK at this stage. We handle allocations of zero in PerformOneGeneration().
            int idx = _rng.Next(specieCount - 1);
            idx = idx == _bestSpecieIdx ? idx + 1 : idx;

            if (specieStatsArr[idx]._targetSizeInt > 0)
            {
                specieStatsArr[idx]._targetSizeInt--;
            }
            else
            {   // Scan forward from this specie to find a suitable one.
                bool done = false;
                idx++;
                for (; idx < specieCount; idx++)
                {
                    if (idx != _bestSpecieIdx && specieStatsArr[idx]._targetSizeInt > 0)
                    {
                        specieStatsArr[idx]._targetSizeInt--;
                        done = true;
                        break;
                    }
                }

                // Scan forward from start of species list.
                if (!done)
                {
                    for (int i = 0; i < specieCount; i++)
                    {
                        if (i != _bestSpecieIdx && specieStatsArr[i]._targetSizeInt > 0)
                        {
                            specieStatsArr[i]._targetSizeInt--;
                            done = true;
                            break;
                        }
                    }
                    if (!done)
                    {
                        throw new SharpNeatException("CalcSpecieStats(). Error adjusting target population size down. Is the population size less than or equal to the number of species?");
                    }
                }
            }
        }

        // Now determine the eliteSize for each specie. This is the number of genomes that will remain in a 
        // specie from the current generation and is a proportion of the specie's current size.
        // Also here we calculate the total number of offspring that will need to be generated.
        offspringCount = 0;
        for (int i = 0; i < specieCount; i++)
        {
            // Special case - zero target size.
            if (0 == specieStatsArr[i]._targetSizeInt)
            {
                specieStatsArr[i]._eliteSizeInt = 0;
                continue;
            }

            // Discretize the real size with a probabilistic handling of the fractional part.
            double eliteSizeReal = _specieList[i].GenomeList.Count * _eaParams.ElitismProportion;
            int eliteSizeInt = (int)NumericsUtils.ProbabilisticRound(eliteSizeReal, _rng);

            // Ensure eliteSizeInt is no larger than the current target size (remember it was calculated 
            // against the current size of the specie not its new target size).
            SpecieStats inst = specieStatsArr[i];
            inst._eliteSizeInt = System.Math.Min(eliteSizeInt, inst._targetSizeInt);

            // Ensure the champ specie preserves the champ genome. We do this even if the target size is just 1
            // - which means the champ genome will remain and no offspring will be produced from it, apart from 
            // the (usually small) chance of a cross-species mating.
            if (i == _bestSpecieIdx && inst._eliteSizeInt == 0)
            {
                Debug.Assert(inst._targetSizeInt != 0, "Zero target size assigned to champ specie.");
                inst._eliteSizeInt = 1;
            }

            // Now we can determine how many offspring to produce for the specie.
            inst._offspringCount = inst._targetSizeInt - inst._eliteSizeInt;
            offspringCount += inst._offspringCount;

            // While we're here we determine the split between asexual and sexual reproduction. Again using 
            // some probabilistic logic to compensate for any rounding bias.
            double offspringAsexualCountReal = (double)inst._offspringCount * _eaParams.OffspringAsexualProportion;
            inst._offspringAsexualCount = (int)NumericsUtils.ProbabilisticRound(offspringAsexualCountReal, _rng);
            inst._offspringSexualCount = inst._offspringCount - inst._offspringAsexualCount;

            // Also while we're here we calculate the selectionSize. The number of the specie's fittest genomes
            // that are selected from to create offspring. This should always be at least 1.
            double selectionSizeReal = _specieList[i].GenomeList.Count * _eaParams.SelectionProportion;
            inst._selectionSizeInt = System.Math.Max(1, (int)NumericsUtils.ProbabilisticRound(selectionSizeReal, _rng));
        }

        return specieStatsArr;
    }

    /// <summary>
    /// Create the required number of offspring genomes, using specieStatsArr as the basis for selecting how
    /// many offspring are produced from each species.
    /// </summary>
    private List<NeatGenome> CreateOffspring(SpecieStats[] specieStatsArr, int offspringCount)
    {
        // Build a DiscreteDistribution for selecting species for cross-species reproduction.
        // While we're in the loop we also pre-build a DiscreteDistribution for each specie;
        // Doing this before the main loop means we have DiscreteDistributions available for
        // all species when performing cross-specie matings.
        int specieCount = specieStatsArr.Length;
        double[] specieFitnessArr = new double[specieCount];
        DiscreteDistribution[] distArr = new DiscreteDistribution[specieCount];

        // Count of species with non-zero selection size.
        // If this is exactly 1 then we skip inter-species mating. One is a special case because for 0 the 
        // species all get an even chance of selection, and for >1 we can just select normally.
        int nonZeroSpecieCount = 0;
        for (int i = 0; i < specieCount; i++)
        {
            // Array of probabilities for specie selection. Note that some of these probabilities can be zero, but at least one of them won't be.
            SpecieStats inst = specieStatsArr[i];
            specieFitnessArr[i] = inst._selectionSizeInt;

            if (0 == inst._selectionSizeInt)
            {
                // Skip building a DiscreteDistribution for species that won't be selected from.
                distArr[i] = null;
                continue;
            }

            nonZeroSpecieCount++;

            // For each specie we build a DiscreteDistribution for genome selection within 
            // that specie. Fitter genomes have higher probability of selection.
            List<NeatGenome> genomeList = _specieList[i].GenomeList;
            double[] probabilities = new double[inst._selectionSizeInt];
            for (int j = 0; j < inst._selectionSizeInt; j++)
            {
                probabilities[j] = genomeList[j].EvaluationInfo.Fitness;
            }
            distArr[i] = new DiscreteDistribution(probabilities);
        }

        // Complete construction of DiscreteDistribution for specie selection.
        DiscreteDistribution rwlSpecies = new DiscreteDistribution(specieFitnessArr);

        // Produce offspring from each specie in turn and store them in offspringList.
        List<NeatGenome> offspringList = new List<NeatGenome>(offspringCount);
        for (int specieIdx = 0; specieIdx < specieCount; specieIdx++)
        {
            SpecieStats inst = specieStatsArr[specieIdx];
            List<NeatGenome> genomeList = _specieList[specieIdx].GenomeList;

            // Get DiscreteDistribution for genome selection.
            DiscreteDistribution dist = distArr[specieIdx];

            // --- Produce the required number of offspring from asexual reproduction.
            for (int i = 0; i < inst._offspringAsexualCount; i++)
            {
                int genomeIdx = DiscreteDistribution.Sample(_rng, dist);
                NeatGenome offspring = genomeList[genomeIdx].CreateOffspring(generation);
                offspringList.Add(offspring);
            }
            _stats._asexualOffspringCount += (ulong)inst._offspringAsexualCount;

            // --- Produce the required number of offspring from sexual reproduction.
            // Cross-specie mating.
            // If nonZeroSpecieCount is exactly 1 then we skip inter-species mating. One is a special case because
            // for 0 the  species all get an even chance of selection, and for >1 we can just select species normally.
            int crossSpecieMatings = nonZeroSpecieCount == 1 ? 0 :
                                        (int)NumericsUtils.ProbabilisticRound(_eaParams.InterspeciesMatingProportion
                                                                        * inst._offspringSexualCount, _rng);
            _stats._sexualOffspringCount += (ulong)(inst._offspringSexualCount - crossSpecieMatings);
            _stats._interspeciesOffspringCount += (ulong)crossSpecieMatings;

            // An index that keeps track of how many offspring have been produced in total.
            int matingsCount = 0;
            for (; matingsCount < crossSpecieMatings; matingsCount++)
            {
                NeatGenome offspring = CreateOffspring_CrossSpecieMating(dist, distArr, rwlSpecies, specieIdx, genomeList);
                offspringList.Add(offspring);
            }

            // For the remainder we use normal intra-specie mating.
            // Test for special case - we only have one genome to select from in the current specie. 
            if (1 == inst._selectionSizeInt)
            {
                // Fall-back to asexual reproduction.
                for (; matingsCount < inst._offspringSexualCount; matingsCount++)
                {
                    int genomeIdx = DiscreteDistribution.Sample(_rng, dist);
                    NeatGenome offspring = genomeList[genomeIdx].CreateOffspring(generation);
                    offspringList.Add(offspring);
                }
            }
            else
            {
                // Remainder of matings are normal within-specie.
                for (; matingsCount < inst._offspringSexualCount; matingsCount++)
                {
                    // Select parent 1.
                    int parent1Idx = DiscreteDistribution.Sample(_rng, dist);
                    NeatGenome parent1 = genomeList[parent1Idx];

                    // Remove selected parent from set of possible outcomes.
                    DiscreteDistribution distTmp = dist.RemoveOutcome(parent1Idx);

                    // Test for existence of at least one more parent to select.
                    if (0 != distTmp.Probabilities.Length)
                    {   // Get the two parents to mate.
                        int parent2Idx = DiscreteDistribution.Sample(_rng, distTmp);
                        NeatGenome parent2 = genomeList[parent2Idx];
                        NeatGenome offspring = parent1.CreateOffspring(parent2, generation);
                        offspringList.Add(offspring);
                    }
                    else
                    {   // No other parent has a non-zero selection probability (they all have zero fitness).
                        // Fall back to asexual reproduction of the single genome with a non-zero fitness.
                        NeatGenome offspring = parent1.CreateOffspring(generation);
                        offspringList.Add(offspring);
                    }
                }
            }
        }

        _stats._totalOffspringCount += (ulong)offspringCount;
        return offspringList;
    }

    /// <summary>
    /// Cross specie mating.
    /// </summary>
    /// <param name="dist">DiscreteDistribution for selecting genomes in the current specie.</param>
    /// <param name="distArr">Array of DiscreteDistribution objects for genome selection. One for each specie.</param>
    /// <param name="rwlSpecies">DiscreteDistribution for selecting species. Based on relative fitness of species.</param>
    /// <param name="currentSpecieIdx">Current specie's index in _specieList</param>
    /// <param name="genomeList">Current specie's genome list.</param>
    private NeatGenome CreateOffspring_CrossSpecieMating(DiscreteDistribution dist,
                                                      DiscreteDistribution[] distArr,
                                                      DiscreteDistribution rwlSpecies,
                                                      int currentSpecieIdx,
                                                      IList<NeatGenome> genomeList)
    {
        // Select parent from current specie.
        int parent1Idx = DiscreteDistribution.Sample(_rng, dist);

        // Select specie other than current one for 2nd parent genome.
        DiscreteDistribution distSpeciesTmp = rwlSpecies.RemoveOutcome(currentSpecieIdx);
        int specie2Idx = DiscreteDistribution.Sample(_rng, distSpeciesTmp);

        // Select a parent genome from the second specie.
        int parent2Idx = DiscreteDistribution.Sample(_rng, distArr[specie2Idx]);

        // Get the two parents to mate.
        NeatGenome parent1 = genomeList[parent1Idx];
        NeatGenome parent2 = _specieList[specie2Idx].GenomeList[parent2Idx];
        return parent1.CreateOffspring(parent2, generation);
    }

    #endregion

    #region Private Methods [Low Level Helper Methods]

    /// <summary>
    /// Updates _currentBestGenome and _bestSpecieIdx, these are the fittest genome and index of the specie
    /// containing the fittest genome respectively.
    /// 
    /// This method assumes that all specie genomes are sorted fittest first and can therefore save much work
    /// by not having to scan all genomes.
    /// Note. We may have several genomes with equal best fitness, we just select one of them in that case.
    /// </summary>
    protected void UpdateBestGenome()
    {
        // If all genomes have the same fitness (including zero) then we simply return the first genome.
        NeatGenome bestGenome = null;
        double bestFitness = -1.0;
        int bestSpecieIdx = -1;

        int count = _specieList.Count;
        for (int i = 0; i < count; i++)
        {
            // Get the specie's first genome. Genomes are sorted, therefore this is also the fittest 
            // genome in the specie.
            NeatGenome genome = _specieList[i].GenomeList[0];
            if (genome.EvaluationInfo.Fitness > bestFitness)
            {
                bestGenome = genome;
                bestFitness = genome.EvaluationInfo.Fitness;
                bestSpecieIdx = i;
            }
        }

        _currentBestGenome = bestGenome;
        _bestSpecieIdx = bestSpecieIdx;
    }

    /// <summary>
    /// Updates the NeatAlgorithmStats object.
    /// </summary>
    private void UpdateStats()
    {
        lock (_stats)
        {
            _stats._generation = generation;
            _stats._totalEvaluationCount = (generation + 1)  * (uint)gamesToCreate;

            // Evaluation per second.
            System.DateTime now = System.DateTime.Now;
            System.TimeSpan duration = now - _stats._evalsPerSecLastSampleTime;

            // To smooth out the evals per sec statistic we only update if at least 1 second has elapsed 
            // since it was last updated.
            if (duration.Ticks > 9999)
            {
                long evalsSinceLastUpdate = (long)(_stats._totalEvaluationCount - _stats._evalsCountAtLastUpdate);
                _stats._evaluationsPerSec = (int)((evalsSinceLastUpdate * 1e7) / duration.Ticks);

                // Reset working variables.
                _stats._evalsCountAtLastUpdate = (generation + 1)  *(uint)gamesToCreate; 
                _stats._evalsPerSecLastSampleTime = now;
            }

            // Fitness and complexity stats.
            double totalFitness = genomeList[0].EvaluationInfo.Fitness;
            double totalComplexity = genomeList[0].Complexity;
            double maxComplexity = totalComplexity;

            int count = genomeList.Count;
            for (int i = 1; i < count; i++)
            {
                totalFitness += genomeList[i].EvaluationInfo.Fitness;
                totalComplexity += genomeList[i].Complexity;
                maxComplexity = System.Math.Max(maxComplexity, genomeList[i].Complexity);
            }

            _stats._maxFitness = _currentBestGenome.EvaluationInfo.Fitness;
            _stats._meanFitness = totalFitness / count;

            _stats._maxComplexity = maxComplexity;
            _stats._meanComplexity = totalComplexity / count;

            // Specie champs mean fitness.
            double totalSpecieChampFitness = _specieList[0].GenomeList[0].EvaluationInfo.Fitness;
            int specieCount = _specieList.Count;
            for (int i = 1; i < specieCount; i++)
            {
                totalSpecieChampFitness += _specieList[i].GenomeList[0].EvaluationInfo.Fitness;
            }
            _stats._meanSpecieChampFitness = totalSpecieChampFitness / specieCount;

            // Moving averages.
            _stats._prevBestFitnessMA = _stats._bestFitnessMA.Mean;
            _stats._bestFitnessMA.Enqueue(_stats._maxFitness);

            _stats._prevMeanSpecieChampFitnessMA = _stats._meanSpecieChampFitnessMA.Mean;
            _stats._meanSpecieChampFitnessMA.Enqueue(_stats._meanSpecieChampFitness);

            _stats._prevComplexityMA = _stats._complexityMA.Mean;
            _stats._complexityMA.Enqueue(_stats._meanComplexity);
        }
    }

    /// <summary>
    /// Sorts the genomes within each species fittest first, secondary sorts on age.
    /// </summary>
    private void SortSpecieGenomes()
    {
        int minSize = _specieList[0].GenomeList.Count;
        int maxSize = minSize;
        int specieCount = _specieList.Count;

        for (int i = 0; i < specieCount; i++)
        {
            SortUtils.SortUnstable(_specieList[i].GenomeList, GenomeFitnessComparer<NeatGenome>.Singleton, _rng);
            minSize = System.Math.Min(minSize, _specieList[i].GenomeList.Count);
            maxSize = System.Math.Max(maxSize, _specieList[i].GenomeList.Count);
        }

        // Update stats.
        _stats._minSpecieSize = minSize;
        _stats._maxSpecieSize = maxSize;
    }

    /// <summary>
    /// Clear the genome list within each specie.
    /// </summary>
    private void ClearAllSpecies()
    {
        foreach (Specie<NeatGenome> specie in _specieList)
        {
            specie.GenomeList.Clear();
        }
    }

    /// <summary>
    /// Rebuild _genomeList from genomes held within the species.
    /// </summary>
    private void RebuildGenomeList()
    {
        genomeList.Clear();
        foreach (Specie<NeatGenome> specie in _specieList)
        {
            genomeList.AddRange(specie.GenomeList);
        }
    }

    /// <summary>
    /// Trims the genomeList in each specie back to the number of elite genomes specified in
    /// specieStatsArr. Returns true if there are empty species following trimming.
    /// </summary>
    private bool TrimSpeciesBackToElite(SpecieStats[] specieStatsArr)
    {
        bool emptySpeciesFlag = false;
        int count = _specieList.Count;
        for (int i = 0; i < count; i++)
        {
            Specie<NeatGenome> specie = _specieList[i];
            SpecieStats stats = specieStatsArr[i];

            int removeCount = specie.GenomeList.Count - stats._eliteSizeInt;
            specie.GenomeList.RemoveRange(stats._eliteSizeInt, removeCount);

            if (0 == stats._eliteSizeInt)
            {
                emptySpeciesFlag = true;
            }
        }
        return emptySpeciesFlag;
    }

    #endregion

    #region Private Methods [Debugging]

    /// <summary>
    /// Returns true if there is one or more empty species.
    /// </summary>
    private bool TestForEmptySpecies(IList<Specie<NeatGenome>> specieList)
    {
        foreach (Specie<NeatGenome> specie in specieList)
        {
            if (specie.GenomeList.Count == 0)
            {
                return true;
            }
        }
        return false;
    }


    private static int SumTargetSizeInt(SpecieStats[] specieStatsArr)
    {
        int total = 0;
        foreach (SpecieStats inst in specieStatsArr)
        {
            total += inst._targetSizeInt;
        }
        return total;
    }

    #endregion

    #region InnerClass [SpecieStats]

    class SpecieStats
    {
        // Real/continuous stats.
        public double _meanFitness;
        public double _targetSizeReal;

        // Integer stats.
        public int _targetSizeInt;
        public int _eliteSizeInt;
        public int _offspringCount;
        public int _offspringAsexualCount;
        public int _offspringSexualCount;

        // Selection data.
        public int _selectionSizeInt;
    }

    #endregion
}
