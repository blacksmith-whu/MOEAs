﻿using MOEAPlat.Common;
using MOEAPlat.Encoding;
using MOEAPlat.PlotDialog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/*
 * The details of MOEA/DD and test problems can refer to the following paper
 * 
 * Li K, Deb K, Zhang Q, et al. An Evolutionary Many-Objective Optimization Algorithm Based on Dominance 
 * and Decomposition[J]. IEEE Transactions on Evolutionary Computation, 2015, 19(5):694-716.
 * 
*/

namespace MOEAPlat.Algorithms
{
    public class MOEADD : MultiObjectiveSolver
    {
        int numRanks;
        int[,] rankIdx_;           // index matrix for the non-domination levels
        int[,] subregionIdx_;      // index matrix for subregion record
        double[,] subregionDist_;	// distance matrix for perpendicular distance

        protected List<List<int>> associatedSolution;
        protected List<List<int>> dominateSet;


        Random random = new Random();

        public int p;

        public void initial()
        {
            this.idealpoint = new double[this.numObjectives];
            this.narpoint = new double[this.numObjectives];

            for (int i = 0; i < numObjectives; i++)
            {
                idealpoint[i] = Double.MaxValue;
                narpoint[i] = Double.MinValue;
            }

            initWeight(this.div);
            initialPopulation();
            initNeighbour();

            rankIdx_ = new int[popsize, popsize];
            subregionIdx_ = new int[popsize, popsize];
            subregionDist_ = new double[popsize, popsize];

            for (int i = 0; i < mainpop.Count(); i++)
            {
                subregionIdx_[i, i] = 1;
            }

            for (int i = 0; i < popsize; i++)
            {
                double distance = calculateDistance2(mainpop[i], this.weights[i]);
                subregionDist_[i, i] = distance;
            }

            NSGA.fastNonDominatedSort(mainpop);
            int curRank;
            for (int i = 0; i < popsize; i++)
            {
                curRank = mainpop[i].getRank();
                rankIdx_[curRank, i] = 1;
            }

            associatedSolution = new List<List<int>>();
            dominateSet = new List<List<int>>();
            for (int i = 0; i < this.popsize; i++)
            {
                List<int> temp = new List<int>();
                associatedSolution.Add(temp);

                List<int> temp1 = new List<int>();
                dominateSet.Add(temp1);
            }
        }

        protected void initNeighbour()
        {
            neighbourTable = new List<int[]>(popsize);

            double[,] distancematrix = new double[popsize, popsize];
            for (int i = 0; i < popsize; i++)
            {
                distancematrix[i, i] = 0;
                for (int j = i + 1; j < popsize; j++)
                {
                    distancematrix[i, j] = distance(weights[i], weights[j]);
                    distancematrix[j, i] = distancematrix[i, j];
                }
            }

            for (int i = 0; i < popsize; i++)
            {
                double[] val = new double[popsize];
                for (int j = 0; j < popsize; j++)
                {
                    val[j] = distancematrix[i, j];
                }

                int[] index = Sorting.sorting(val);
                int[] array = new int[this.neighbourSize];
                Array.Copy(index, array, this.neighbourSize);
                neighbourTable.Add(array);
            }
        }

        protected void initWeight(int m)
        {
            this.weights = new List<double[]>();
            if (numObjectives < 6) this.weights = UniPointsGenerator.getMUniDistributedPoint(numObjectives, m);
            else this.weights = UniPointsGenerator.getMaUniDistributedPoint(numObjectives, m, 2);

            this.popsize = this.weights.Count();
        }

        protected void initialPopulation()
        {
            for (int i = 0; i < this.popsize; i++)
            {
                MoChromosome chromosome = this.createChromosome();

                evaluate(chromosome);
                updateReference(chromosome);
                mainpop.Add(chromosome);
            }
        }

        protected override void doSolve()
        {
            initial();
            frm = new plotFrm(mainpop, mop.getName());
            frm.Show();
            frm.Refresh();
            while (!terminated())
            {
                for (int i = 0; i < popsize; i++)
                {
                    MoChromosome offspring;
                    offspring = SBXCrossover(i, true);//GeneticOPDE//GeneticOPSBXCrossover
                    this.evaluate(offspring);
                    updateReference(offspring);
                    updateArchive(offspring);
                }

                if (this.ItrCounter % 10 == 0)
                {
                    frm.refereshPlot(this.ItrCounter, mainpop);
                    frm.Refresh();
                }

                ItrCounter++;
            }
            Common.FileTool.WritetoFile(mainpop, "gen", 1);
            Common.FileTool.WritetoFile(mainpop, "obj", 2);
        }

        public void nondominated_sorting_delete(MoChromosome indiv)
        {

            // find the non-domination level of 'indiv'
            int indivRank = indiv.getRank();

            List<int> curLevel = new List<int>();   // used to keep the solutions in the current non-domination level
            List<int> dominateList = new List<int>();   // used to keep the solutions need to be moved

            for (int i = 0; i < popsize; i++)
            {
                if (rankIdx_[indivRank,i] == 1)
                    curLevel.Add(i);
            }

            int flag;
            // find the solutions belonging to the 'indivRank+1'th level and are dominated by 'indiv'
            int investigateRank = indivRank + 1;
            if (investigateRank < numRanks)
            {
                for (int i = 0; i < popsize; i++)
                {
                    if (rankIdx_[investigateRank,i] == 1)
                    {
                        flag = 0;
                        if (checkDominance(indiv, mainpop[i]) == 1)
                        {
                            for (int j = 0; j < curLevel.Count(); j++)
                            {
                                if (checkDominance(mainpop[i], mainpop[curLevel[j]]) == -1)
                                {
                                    flag = 1;
                                    break;
                                }
                            }
                            if (flag == 0)
                            {   // the ith solution can move to the prior level
                                dominateList.Add(i);
                                rankIdx_[investigateRank,i] = 0;
                                rankIdx_[investigateRank - 1,i] = 1;
                                mainpop[i].setRank(investigateRank - 1);
                            }
                        }
                    }
                }
            }

            int curIdx;
            int curListSize = dominateList.Count();
            while (curListSize != 0)
            {
                curLevel.Clear();
                for (int i = 0; i < popsize; i++)
                {
                    if (rankIdx_[investigateRank,i] == 1)
                        curLevel.Add(i);
                }
                investigateRank = investigateRank + 1;

                if (investigateRank < numRanks)
                {
                    for (int i = 0; i < curListSize; i++)
                    {
                        curIdx = dominateList[i];
                        for (int j = 0; j < popsize; j++)
                        {
                            if (j == popsize)
                            {
                                //System.out.println("Fuck me!!!");
                            }
                            if (rankIdx_[investigateRank,j] == 1)
                            {
                                flag = 0;
                                if (checkDominance(mainpop[curIdx], mainpop[j]) == 1)
                                {
                                    for (int k = 0; k < curLevel.Count(); k++)
                                    {
                                        if (checkDominance(mainpop[j], mainpop[curLevel[k]]) == -1)
                                        {
                                            flag = 1;
                                            break;
                                        }
                                    }
                                    if (flag == 0)
                                    {
                                        dominateList.Add(j);
                                        rankIdx_[investigateRank,j] = 0;
                                        rankIdx_[investigateRank - 1,j] = 1;
                                        mainpop[j].setRank(investigateRank - 1);
                                    }
                                }
                            }
                        }
                    }
                }
                for (int i = 0; i < curListSize; i++)
                    dominateList.RemoveAt(0);

                curListSize = dominateList.Count();
            }

        }

        public void updateArchive(MoChromosome indiv)
        {

            // find the location of 'indiv'
            updateReference(indiv);

            setLocation(indiv);

            int location = indiv.subProbNo;

            numRanks = nondominated_sorting_add(indiv);

            if (numRanks == 1)
            {
                deleteRankOne(indiv, location);
            }
            else
            {
                List<MoChromosome> lastFront = new List<MoChromosome>();
                int frontSize = countRankOnes(numRanks - 1);
                if (frontSize == 0)
                {   // the last non-domination level only contains 'indiv'
                    frontSize++;
                    lastFront.Add(indiv);
                }
                else
                {
                    for (int i = 0; i < popsize; i++)
                    {
                        if (rankIdx_[numRanks - 1,i] == 1)
                            lastFront.Add(mainpop[i]);
                    }
                    if (indiv.getRank() == (numRanks - 1))
                    {
                        frontSize++;
                        lastFront.Add(indiv);
                    }
                }

                if (frontSize == 1 && lastFront[0].equals(indiv))
                {   // the last non-domination level only has 'indiv'
                    int curNC = countOnes(location);
                    if (curNC > 0)
                    {   // if the subregion of 'indiv' has other solution, drop 'indiv'
                        nondominated_sorting_delete(indiv);
                        return;
                    }
                    else
                    {   // if the subregion of 'indiv' has no solution, keep 'indiv'
                        deleteCrowdRegion1(indiv, location);
                    }
                }
                else if (frontSize == 1 && !lastFront[0].equals(indiv))
                { // the last non-domination level only has one solution, but not 'indiv'
                    int targetIdx = findPosition(lastFront[0]);
                    int parentLocation = findRegion(targetIdx);
                    int curNC = countOnes(parentLocation);
                    if (parentLocation == location)
                        curNC++;

                    if (curNC == 1)
                    {   // the subregion only has the solution 'targetIdx', keep solution 'targetIdx'
                        deleteCrowdRegion2(indiv, location);
                    }
                    else
                    {   // the subregion contains some other solutions, drop solution 'targetIdx'
                        int indivRank = indiv.getRank();
                        int targetRank = mainpop[targetIdx].getRank();
                        rankIdx_[targetRank,targetIdx] = 0;
                        rankIdx_[indivRank,targetIdx] = 1;

                        MoChromosome targetSol = this.createChromosome();
                        mainpop[targetIdx].copyTo(targetSol);

                        //Solution targetSol = new Solution(population_.get(targetIdx));
                        indiv.copyTo(mainpop[targetIdx]);
                        //mainpop.chromosomes.replace(targetIdx, indiv);

                        subregionIdx_[parentLocation,targetIdx] = 0;
                        subregionIdx_[location,targetIdx] = 1;

                        // update the non-domination level structure
                        nondominated_sorting_delete(targetSol);
                    }
                }
                else
                {
                    double indivFitness = fitnessFunction(indiv, location);

                    // find the index of the solution in the last non-domination level, and its corresponding subregion
                    int[] idxArray = new int[frontSize];
                    int[] regionArray = new int[frontSize];

                    for (int i = 0; i < frontSize; i++)
                    {
                        idxArray[i] = findPosition(lastFront[i]);
                        if (idxArray[i] == -1)
                            regionArray[i] = location;
                        else
                            regionArray[i] = findRegion(idxArray[i]);
                    }

                    // find the most crowded subregion, if more than one exist, keep them in 'crowdList'
                    List<int> crowdList = new List<int>();

                    int crowdIdx;
                    int nicheCount = countOnes(regionArray[0]);
                    if (regionArray[0] == location)
                        nicheCount++;
                    crowdList.Add(regionArray[0]);
                    for (int i = 1; i < frontSize; i++)
                    {
                        int curSize = countOnes(regionArray[i]);
                        if (regionArray[i] == location)
                            curSize++;
                        if (curSize > nicheCount)
                        {
                            crowdList.Clear();
                            nicheCount = curSize;
                            crowdList.Add(regionArray[i]);
                        }
                        else if (curSize == nicheCount)
                        {
                            crowdList.Add(regionArray[i]);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    // find the index of the most crowded subregion
                    if (crowdList.Count() == 1)
                    {
                        crowdIdx = crowdList[0];
                    }
                    else
                    {
                        int listLength = crowdList.Count();
                        crowdIdx = crowdList[0];
                        double sumFitness1 = sumFitness(crowdIdx);
                        if (crowdIdx == location)
                            sumFitness1 = sumFitness1 + indivFitness;
                        for (int i = 1; i < listLength; i++)
                        {
                            int curIdx = crowdList[i];
                            double curFitness = sumFitness(curIdx);
                            if (curIdx == location)
                                curFitness = curFitness + indivFitness;
                            if (curFitness > sumFitness1)
                            {
                                crowdIdx = curIdx;
                                sumFitness1 = curFitness;
                            }
                        }
                    }

                    if (nicheCount == 0)
                    {

                    }
				    else if (nicheCount == 1)
                    { // if the subregion of each solution in the last non-domination level only has one solution, keep them all
                        deleteCrowdRegion2(indiv, location);
                    }
                    else
                    { // delete the worst solution from the most crowded subregion in the last non-domination level
                        List<int> list = new List<int>();
                        for (int i = 0; i < frontSize; i++)
                        {
                            if (regionArray[i] == crowdIdx)
                                list.Add(i);
                        }
                        if (list.Count() == 0)
                        {
                            //System.out.println("Cannot happen!!!");
                        }
                        else
                        {
                            double maxFitness, curFitness;
                            int targetIdx = list[0];
                            if (idxArray[targetIdx] == -1)
                                maxFitness = indivFitness;
                            else
                                maxFitness = fitnessFunction(mainpop[idxArray[targetIdx]], crowdIdx);
                            for (int i = 1; i < list.Count(); i++)
                            {
                                int curIdx = list[i];
                                if (idxArray[curIdx] == -1)
                                    curFitness = indivFitness;
                                else
                                    curFitness = fitnessFunction(mainpop[idxArray[curIdx]], crowdIdx);
                                if (curFitness > maxFitness)
                                {
                                    targetIdx = curIdx;
                                    maxFitness = curFitness;
                                }
                            }
                            if (idxArray[targetIdx] == -1)
                            {
                                nondominated_sorting_delete(indiv);
                                return;
                            }
                            else
                            {
                                int indivRank = indiv.getRank();
                                int targetRank = mainpop[idxArray[targetIdx]].getRank();
                                rankIdx_[targetRank,idxArray[targetIdx]] = 0;
                                rankIdx_[indivRank,idxArray[targetIdx]] = 1;

                                MoChromosome targetSol = this.createChromosome();
                                mainpop[targetIdx].copyTo(targetSol);

                                //Solution targetSol = new Solution(population_.get(idxArray[targetIdx]));
                                indiv.copyTo(mainpop[targetIdx]);

                                //population_.replace(idxArray[targetIdx], indiv);
                                subregionIdx_[crowdIdx,idxArray[targetIdx]] = 0;
                                subregionIdx_[location,idxArray[targetIdx]] = 1;

                                // update the non-domination level structure
                                nondominated_sorting_delete(targetSol);
                            }
                        }
                    }
                }
            }

            return;
        }

        public int nondominated_sorting_add(MoChromosome indiv)
        {

            int flag = 0;
            int flag1, flag2, flag3;

            // count the number of non-domination levels
            int num_ranks = 0;
            List<int> frontSize = new List<int>();
            for (int i = 0; i < popsize; i++)
            {
                int rankCount = countRankOnes(i);
                if (rankCount != 0)
                {
                    frontSize.Add(rankCount);
                    num_ranks++;
                }
                else
                {
                    break;
                }
            }

            List<int> dominateList = new List<int>(); // used to keep the solutions dominated by 'indiv'
            int level = 0;
            for (int i = 0; i < num_ranks; i++)
            {
                level = i;
                if (flag == 1)
                {   // 'indiv' is non-dominated with all solutions in the ith non-domination level, then 'indiv' belongs to the ith level
                    indiv.setRank(i - 1);
                    return num_ranks;
                }
                else if (flag == 2)
                {   // 'indiv' dominates some solutions in the ith level, but is non-dominated with some others, then 'indiv' belongs to the ith level, and move the dominated solutions to the next level
                    indiv.setRank(i - 1);

                    int prevRank = i - 1;

                    // process the solutions belong to 'prevRank'th level and are dominated by 'indiv' ==> move them to 'prevRank+1'th level and find the solutions dominated by them
                    int curIdx;
                    int newRank = prevRank + 1;
                    int curListSize = dominateList.Count();
                    for (int j = 0; j < curListSize; j++)
                    {
                        curIdx = dominateList[j];
                        rankIdx_[prevRank,curIdx] = 0;
                        rankIdx_[newRank,curIdx] = 1;
                        mainpop[curIdx].setRank(newRank);
                    }
                    for (int j = 0; j < popsize; j++)
                    {
                        if (rankIdx_[newRank,j] == 1)
                        {
                            for (int k = 0; k < curListSize; k++)
                            {
                                curIdx = dominateList[k];
                                if (checkDominance(mainpop[curIdx], mainpop[j]) == 1)
                                {
                                    dominateList.Add(j);
                                    break;
                                }

                            }
                        }
                    }
                    for (int j = 0; j < curListSize; j++)
                        dominateList.RemoveAt(0);

                    // if there are still some other solutions moved to the next level, check their domination situation in their new level
                    prevRank = newRank;
                    newRank = newRank + 1;
                    curListSize = dominateList.Count();
                    if (curListSize == 0)
                        return num_ranks;
                    else
                    {
                        int allFlag = 0;
                        do
                        {
                            for (int j = 0; j < curListSize; j++)
                            {
                                curIdx = dominateList[j];
                                rankIdx_[prevRank,curIdx] = 0;
                                rankIdx_[newRank,curIdx] = 1;
                                mainpop[curIdx].setRank(newRank);
                            }
                            for (int j = 0; j < popsize; j++)
                            {
                                if (rankIdx_[newRank,j] == 1)
                                {
                                    for (int k = 0; k < curListSize; k++)
                                    {
                                        curIdx = dominateList[k];
                                        if (checkDominance(mainpop[curIdx], mainpop[j]) == 1)
                                        {
                                            dominateList.Add(j);
                                            break;
                                        }
                                    }
                                }
                            }
                            for (int j = 0; j < curListSize; j++)
                                dominateList.RemoveAt(0);

                            curListSize = dominateList.Count();
                            if (curListSize != 0)
                            {
                                prevRank = newRank;
                                newRank = newRank + 1;
                                if (curListSize == frontSize[prevRank])
                                {   // if all solutions in the 'prevRank'th level are dominated by the newly added solution, move them all to the next level
                                    allFlag = 1;
                                    break;
                                }
                            }
                        } while (curListSize != 0);

                        if (allFlag == 1)
                        {   // move the solutions after the 'prevRank'th level to their next levels
                            int remainSize = num_ranks - prevRank;
                            int[, ] tempRecord = new int[remainSize, popsize];

                            int tempIdx = 0;
                            for (int j = 0; j < dominateList.Count(); j++)
                            {
                                tempRecord[0, tempIdx] = dominateList[j];
                                tempIdx++;
                            }

                            int k = 1;
                            int curRank = prevRank + 1;
                            while (curRank < num_ranks)
                            {
                                tempIdx = 0;
                                for (int j = 0; j < popsize; j++)
                                {
                                    if (rankIdx_[curRank,j] == 1)
                                    {
                                        tempRecord[k, tempIdx] = j;
                                        tempIdx++;
                                    }
                                }
                                curRank++;
                                k++;
                            }

                            k = 0;
                            curRank = prevRank;
                            while (curRank < num_ranks)
                            {
                                int level_size = frontSize[curRank];

                                int tempRank;
                                for (int j = 0; j < level_size; j++)
                                {
                                    curIdx = tempRecord[k, j];
                                    tempRank = mainpop[curIdx].getRank();
                                    newRank = tempRank + 1;
                                    mainpop[curIdx].setRank(newRank);

                                    rankIdx_[tempRank,curIdx] = 0;
                                    rankIdx_[newRank,curIdx] = 1;
                                }
                                curRank++;
                                k++;
                            }
                            num_ranks++;
                        }

                        if (newRank == num_ranks)
                            num_ranks++;

                        return num_ranks;
                    }
                }
                else if (flag == 3 || flag == 0)
                {   // if 'indiv' is dominated by some solutions in the ith level, skip it, and term to the next level
                    flag1 = flag2 = flag3 = 0;
                    for (int j = 0; j < popsize; j++)
                    {
                        if (rankIdx_[i,j] == 1)
                        {
                            switch (checkDominance(indiv, mainpop[j]))
                            {
                                case 1:
                                    {
                                        flag1 = 1;
                                        dominateList.Add(j);
                                        break;
                                    }
                                case 0:
                                    {
                                        flag2 = 1;
                                        break;
                                    }
                                case -1:
                                    {
                                        flag3 = 1;
                                        break;
                                    }
                            }

                            if (flag3 == 1)
                            {
                                flag = 3;
                                break;
                            }
                            else if (flag1 == 0 && flag2 == 1)
                                flag = 1;
                            else if (flag1 == 1 && flag2 == 1)
                                flag = 2;
                            else if (flag1 == 1 && flag2 == 0)
                                flag = 4;
                            else
                                continue;
                        }
                    }

                }
                else
                {   // (flag == 4) if 'indiv' dominates all solutions in the ith level, solutions in the current level and beyond move their current next levels
                    indiv.setRank(i - 1);
                    i = i - 1;
                    int remainSize = num_ranks - i;
                    int[, ] tempRecord = new int[remainSize, popsize];

                    int k = 0;
                    while (i < num_ranks)
                    {
                        int tempIdx = 0;
                        for (int j = 0; j < popsize; j++)
                        {
                            if (rankIdx_[i,j] == 1)
                            {
                                tempRecord[k, tempIdx] = j;
                                tempIdx++;
                            }
                        }
                        i++;
                        k++;
                    }

                    k = 0;
                    i = indiv.getRank();
                    while (i < num_ranks)
                    {
                        int level_size = frontSize[i];

                        int curIdx;
                        int curRank, newRank;
                        for (int j = 0; j < level_size; j++)
                        {
                            curIdx = tempRecord[k, j];
                            curRank = mainpop[curIdx].getRank();
                            newRank = curRank + 1;
                            mainpop[curIdx].setRank(newRank);

                            rankIdx_[curRank,curIdx] = 0;
                            rankIdx_[newRank,curIdx] = 1;
                        }
                        i++;
                        k++;
                    }
                    num_ranks++;

                    return num_ranks;
                }
            }
            // if flag is still 3 after the for-loop, it means that 'indiv' is in the current last level
            if (flag == 1)
            {
                indiv.setRank(level);
            }
            else if (flag == 2)
            {
                indiv.setRank(level);

                int curIdx;
                int tempSize = dominateList.Count();
                for (int i = 0; i < tempSize; i++)
                {
                    curIdx = dominateList[i];
                    mainpop[curIdx].setRank(level + 1);

                    rankIdx_[level,curIdx] = 0;
                    rankIdx_[level + 1,curIdx] = 1;
                }
                num_ranks++;
            }
            else if (flag == 3)
            {
                indiv.setRank(level + 1);
                num_ranks++;
            }
            else
            {
                indiv.setRank(level);
                for (int i = 0; i < popsize; i++)
                {
                    if (rankIdx_[level,i] == 1)
                    {
                        mainpop[i].setRank(level + 1);

                        rankIdx_[level,i] = 0;
                        rankIdx_[level + 1,i] = 1;
                    }
                }
                num_ranks++;
            }

            return num_ranks;
        }


        public int countRankOnes(int level)
        {
            int count = 0;
            for (int i = 0; i < popsize; i++)
            {
                if (rankIdx_[level, i] == 1)
                    count++;
            }

            return count;
        }

        public int checkDominance(MoChromosome a, MoChromosome b)
        {
            if (a.dominates(b)) return 1;
            if (b.dominates(a)) return -1;
            return 0;

        }

        public int countOnes(int location)
        {

            int count = 0;
            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[location,i] == 1)
                    count++;
            }

            return count;
        }

        public void deleteCrowdRegion1(MoChromosome indiv, int location)
        {

            // find the most crowded subregion, if more than one such subregion exists, keep them in the crowdList
            List<int> crowdList = new List<int>();
            int crowdIdx;
            int nicheCount = countOnes(0);
            crowdList.Add(0);
            for (int i = 1; i < popsize; i++)
            {
                int curSize = countOnes(i);
                if (curSize > nicheCount)
                {
                    crowdList.Clear();
                    nicheCount = curSize;
                    crowdList.Add(i);
                }
                else if (curSize == nicheCount)
                {
                    crowdList.Add(i);
                }
                else
                {
                    continue;
                }
            }
            // find the index of the crowded subregion
            if (crowdList.Count() == 1)
            {
                crowdIdx = crowdList[0];
            }
            else
            {
                int listLength = crowdList.Count();
                crowdIdx = crowdList[0];
                double sumFitness1 = sumFitness(crowdIdx);
                for (int i = 1; i < listLength; i++)
                {
                    int curIdx = crowdList[i];
                    double curFitness = sumFitness(curIdx);
                    if (curFitness > sumFitness1)
                    {
                        crowdIdx = curIdx;
                        sumFitness1 = curFitness;
                    }
                }
            }

            // find the solution indices within the 'crowdIdx' subregion
            List<int> indList = new List<int>();
            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[crowdIdx,i] == 1)
                    indList.Add(i);
            }

            // find the solution with the largest rank
            List<int> maxRankList = new List<int>();
            int maxRank = mainpop[indList[0]].getRank();
            maxRankList.Add(indList[0]);
            for (int i = 1; i < indList.Count(); i++)
            {
                int curRank = mainpop[indList[i]].getRank();
                if (curRank > maxRank)
                {
                    maxRankList.Clear();
                    maxRank = curRank;
                    maxRankList.Add(indList[i]);
                }
                else if (curRank == maxRank)
                {
                    maxRankList.Add(indList[i]);
                }
                else
                {
                    continue;
                }
            }

            // find the solution with the largest rank and worst fitness
            int rankSize = maxRankList.Count();
            int targetIdx = maxRankList[0];
            double maxFitness = fitnessFunction(mainpop[targetIdx], crowdIdx);
            for (int i = 1; i < rankSize; i++)
            {
                int curIdx = maxRankList[i];
                double curFitness = fitnessFunction(mainpop[curIdx], crowdIdx);
                if (curFitness > maxFitness)
                {
                    targetIdx = curIdx;
                    maxFitness = curFitness;
                }
            }

            int indivRank = indiv.getRank();
            int targetRank = mainpop[targetIdx].getRank();
            rankIdx_[targetRank,targetIdx] = 0;
            rankIdx_[indivRank,targetIdx] = 1;

            MoChromosome targetSol = this.createChromosome();
            mainpop[targetIdx].copyTo(targetSol);

            //Solution targetSol = new Solution(population_.get(targetIdx));

            //population_.replace(targetIdx, indiv);

            indiv.copyTo(mainpop[targetIdx]);

            subregionIdx_[crowdIdx,targetIdx] = 0;
            subregionIdx_[location,targetIdx] = 1;

            // update the non-domination level structure
            nondominated_sorting_delete(targetSol);

        }

        /**
         * delete a solution from the most crowded subregion (this function happens when: it should delete the solution
         * in the 'parentLocation' subregion, but since this subregion only has one solution, it should be kept)
         * 
         * @param indiv
         * @param location
         */
        public void deleteCrowdRegion2(MoChromosome indiv, int location)
        {

            double indivFitness = fitnessFunction(indiv, location);

            // find the most crowded subregion, if there are more than one, keep them in crowdList
            List<int> crowdList = new List<int>();
            int crowdIdx;
            int nicheCount = countOnes(0);
            if (location == 0)
                nicheCount++;
            crowdList.Add(0);
            for (int i = 1; i < popsize; i++)
            {
                int curSize = countOnes(i);
                if (location == i)
                    curSize++;
                if (curSize > nicheCount)
                {
                    crowdList.Clear();
                    nicheCount = curSize;
                    crowdList.Add(i);
                }
                else if (curSize == nicheCount)
                {
                    crowdList.Add(i);
                }
                else
                {
                    continue;
                }
            }
            // determine the index of the crowded subregion
            if (crowdList.Count() == 1)
            {
                crowdIdx = crowdList[0];
            }
            else
            {
                int listLength = crowdList.Count();
                crowdIdx = crowdList[0];
                double sumFitness1 = sumFitness(crowdIdx);
                if (crowdIdx == location)
                    sumFitness1 = sumFitness1 + indivFitness;
                for (int i = 1; i < listLength; i++)
                {
                    int curIdx = crowdList[i];
                    double curFitness = sumFitness(curIdx);
                    if (curIdx == location)
                        curFitness = curFitness + indivFitness;
                    if (curFitness > sumFitness1)
                    {
                        crowdIdx = curIdx;
                        sumFitness1 = curFitness;
                    }
                }
            }

            // find the solution indices within the 'crowdIdx' subregion
            List<int> indList = new List<int>();
            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[crowdIdx,i] == 1)
                    indList.Add(i);
            }
            if (crowdIdx == location)
            {
                int temp = -1;
                indList.Add(temp);
            }

            // find the solution with the largest rank
            List<int> maxRankList = new List<int>();
            int maxRank = mainpop[indList[0]].getRank();
            maxRankList.Add(indList[0]);
            for (int i = 1; i < indList.Count(); i++)
            {
                int curRank;
                if (indList[i] == -1)
                    curRank = indiv.getRank();
                else
                    curRank = mainpop[indList[i]].getRank();

                if (curRank > maxRank)
                {
                    maxRankList.Clear();
                    maxRank = curRank;
                    maxRankList.Add(indList[i]);
                }
                else if (curRank == maxRank)
                {
                    maxRankList.Add(indList[i]);
                }
                else
                {
                    continue;
                }
            }

            double maxFitness;
            int rankSize = maxRankList.Count();
            int targetIdx = maxRankList[0];
            if (targetIdx == -1)
                maxFitness = indivFitness;
            else
                maxFitness = fitnessFunction(mainpop[targetIdx], crowdIdx);
            for (int i = 1; i < rankSize; i++)
            {
                double curFitness;
                int curIdx = maxRankList[i];
                if (curIdx == -1)
                    curFitness = indivFitness;
                else
                    curFitness = fitnessFunction(mainpop[curIdx], crowdIdx);

                if (curFitness > maxFitness)
                {
                    targetIdx = curIdx;
                    maxFitness = curFitness;
                }
            }

            if (targetIdx == -1)
            {

                nondominated_sorting_delete(indiv);

                return;
            }
            else
            {
                int indivRank = indiv.getRank();
                int targetRank = mainpop[targetIdx].getRank();
                rankIdx_[targetRank,targetIdx] = 0;
                rankIdx_[indivRank,targetIdx] = 1;

                MoChromosome targetSol = this.createChromosome();
                mainpop[targetIdx].copyTo(targetSol);

                indiv.copyTo(mainpop[targetIdx]);


                //Solution targetSol = new Solution(population_.get(targetIdx));

                //population_.replace(targetIdx, indiv);
                subregionIdx_[crowdIdx,targetIdx] = 0;
                subregionIdx_[location,targetIdx] = 1;

                // update the non-domination level structure of the population
                nondominated_sorting_delete(targetSol);
            }

        }

        public void deleteRankOne(MoChromosome indiv, int location)
        {

            double indivFitness = fitnessFunction(indiv, location);

            // find the most crowded subregion, if there are more than one, keep them in crowdList
            List<int> crowdList = new List<int>();
            int crowdIdx;
            int nicheCount = countOnes(0);
            if (location == 0)
                nicheCount++;
            crowdList.Add(0);
            for (int i = 1; i < popsize; i++)
            {
                int curSize = countOnes(i);
                if (location == i)
                    curSize++;
                if (curSize > nicheCount)
                {
                    crowdList.Clear();
                    nicheCount = curSize;
                    crowdList.Add(i);
                }
                else if (curSize == nicheCount)
                {
                    crowdList.Add(i);
                }
                else
                {
                    continue;
                }
            }
            // determine the index of the crowded subregion
            if (crowdList.Count() == 1)
            {
                crowdIdx = crowdList[0];
            }
            else
            {
                int listLength = crowdList.Count();
                crowdIdx = crowdList[0];
                double sumFitness1 = sumFitness(crowdIdx);
                if (crowdIdx == location)
                    sumFitness1 = sumFitness1 + indivFitness;
                for (int i = 1; i < listLength; i++)
                {
                    int curIdx = crowdList[i];
                    double curFitness = sumFitness(curIdx);
                    if (curIdx == location)
                        curFitness = curFitness + indivFitness;
                    if (curFitness > sumFitness1)
                    {
                        crowdIdx = curIdx;
                        sumFitness1 = curFitness;
                    }
                }
            }

            if (nicheCount == 0)
            {
                //System.out.println("Empty subregion!!!");
            }
            else if (nicheCount == 1)
            { // if every subregion only contains one solution, delete the worst from indiv's subregion
                int targetIdx;
                for (targetIdx = 0; targetIdx < popsize; targetIdx++)
                {
                    if (subregionIdx_[location,targetIdx] == 1)
                        break;
                }

                double prev_func = fitnessFunction(mainpop[targetIdx], location);
                if (indivFitness < prev_func)
                    //population_.replace(targetIdx, indiv);
                    indiv.copyTo(mainpop[targetIdx]);
            }
            else
            {
                if (location == crowdIdx)
                {   // if indiv's subregion is the most crowded one
                    deleteCrowdIndiv_same(location, nicheCount, indivFitness, indiv);
                }
                else
                {
                    int curNC = countOnes(location);
                    int crowdNC = countOnes(crowdIdx);

                    if (crowdNC > (curNC + 1))
                    {   // if the crowdIdx subregion is more crowded, delete one from this subregion
                        deleteCrowdIndiv_diff(crowdIdx, location, crowdNC, indiv);
                    }
                    else if (crowdNC < (curNC + 1))
                    { // crowdNC == curNC, delete one from indiv's subregion
                        deleteCrowdIndiv_same(location, curNC, indivFitness, indiv);
                    }
                    else
                    { // crowdNC == (curNC + 1)
                        if (curNC == 0)
                            deleteCrowdIndiv_diff(crowdIdx, location, crowdNC, indiv);
                        else
                        {
                            //Random rm = new Random();
                            double rnd = random.NextDouble();
                            if (rnd < 0.5)
                                deleteCrowdIndiv_diff(crowdIdx, location, crowdNC, indiv);
                            else
                                deleteCrowdIndiv_same(location, curNC, indivFitness, indiv);
                        }
                    }
                }
            }

        }

        public void deleteCrowdIndiv_same(int crowdIdx, int nicheCount, double indivFitness, MoChromosome indiv)
        {

            // find the solution indices within this crowdIdx subregion
            List<int> indList = new List<int>();
            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[crowdIdx,i] == 1)
                    indList.Add(i);
            }

            // find the solution with the worst fitness value
            int listSize = indList.Count();
            int worstIdx = indList[0];
            double maxFitness = fitnessFunction(mainpop[worstIdx], crowdIdx);
            for (int i = 1; i < listSize; i++)
            {
                int curIdx = indList[i];
                double curFitness = fitnessFunction(mainpop[curIdx], crowdIdx);
                if (curFitness > maxFitness)
                {
                    worstIdx = curIdx;
                    maxFitness = curFitness;
                }
            }

            // if indiv has a better fitness, use indiv to replace the worst one
            if (indivFitness < maxFitness)
                indiv.copyTo(mainpop[worstIdx]); ; //population_.replace(worstIdx, indiv);

        }

        public void deleteCrowdIndiv_diff(int crowdIdx, int curLocation, int nicheCount, MoChromosome indiv)
        {

            // find the solution indices within this crowdIdx subregion
            List<int> indList = new List<int>();
            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[crowdIdx,i] == 1)
                    indList.Add(i);
            }

            // find the solution with the worst fitness value
            int worstIdx = indList[0];
            double maxFitness = fitnessFunction(mainpop[worstIdx], crowdIdx);
            for (int i = 1; i < nicheCount; i++)
            {
                int curIdx = indList[i];
                double curFitness = fitnessFunction(mainpop[curIdx], crowdIdx);
                if (curFitness > maxFitness)
                {
                    worstIdx = curIdx;
                    maxFitness = curFitness;
                }
            }

            // use indiv to replace the worst one
            //population_.replace(worstIdx, indiv);
            indiv.copyTo(mainpop[worstIdx]);
            subregionIdx_[crowdIdx,worstIdx] = 0;
            subregionIdx_[curLocation,worstIdx] = 1;

        }

        public double sumFitness(int location)
        {

            //		double sum = 0;
            //		for (int i = 0; i < this.associatedSolution.get(location).size(); i++) {
            //			if (subregionIdx_[location][i] == 1)
            //				sum = sum + mainpop.chromosomes.get(this.associatedSolution.get(location).get(i)).tchVal;
            //		}

            double sum = 0;
            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[location,i] == 1)
                    sum = sum + fitnessFunction(mainpop[i], location);
            }

            return sum;
        }

        double fitnessFunction(MoChromosome indiv, int idx)//double[] lambda
        {
            double fitness;
            fitness = 0.0;

            fitness = pbiScalarObj(idx, indiv, GlobalValue.IsNormalization);

            return fitness;
        } // fitnessEvaluation

        public int findPosition(MoChromosome indiv)
        {

            for (int i = 0; i < popsize; i++)
            {
                if (indiv.equals(mainpop[i]))
                    return i;
            }

            return -1;
        }

        public int findRegion(int idx)
        {

            for (int i = 0; i < popsize; i++)
            {
                if (subregionIdx_[i,idx] == 1)
                    return i;
            }

            return -1;
        }

        public void setLocation(MoChromosome offSpring)
        {

            double[] v = new double[this.numObjectives];
            double sum = 0, tp;
            for (int i = 0; i < this.numObjectives; i++)
            {
                tp = (offSpring.objectivesValue[i] - idealpoint[i]);///(narpoint[i]+1e-10 - idealpoint[i]);
                v[i] = tp;
            }

            double theta = Double.MaxValue;
            int idx = 0;
            double ta;
            for (int i = 0; i < this.weights.Count(); i++)
            {
                ta = getAngle(v, this.weights[i]);
                if (ta < theta)
                {
                    theta = ta;
                    idx = i;
                }
            }

            offSpring.subProbNo = idx;
            offSpring.angle = theta;

        }

        protected double getAngle(double[] v1, double[] v2)
        {
            double s1 = 0, s2 = 0, s3 = 0;
            for (int i = 0; i < this.numObjectives; i++)
            {
                s1 += v1[i] * v2[i];
                s2 += v1[i] * v1[i];
                s3 += v2[i] * v2[i];
            }
            return Math.Acos(s1 / (Math.Sqrt(s2) * Math.Sqrt(s3)));
        }

        public double calculateDistance2(MoChromosome var, double[] namda)
        {

            // normalize the weight vector (line segment)
            double lenv = 0, mul = 0;
            for (int i = 0; i < numObjectives; i++)
            {
                //mul += ((var.objectivesValue[i] - this.idealpoint[i])/(narpoint[i] + 1e-5 - idealpoint[i]))*namda[i];
                mul += (var.objectivesValue[i] - this.idealpoint[i]) * namda[i];
                lenv += Math.Pow(namda[i], 2);
            }
            double d1 = mul / Math.Sqrt(lenv);

            double d2 = 0;
            for (int i = 0; i < numObjectives; i++)
            {
                //d2 += Math.pow(((var.objectivesValue[i] - this.idealpoint[i])/(narpoint[i] + 1e-5 - idealpoint[i])) - d1 * namda[i]/Math.sqrt(lenv),2);
                d2 += Math.Pow((var.objectivesValue[i] - this.idealpoint[i]) - d1 * namda[i] / Math.Sqrt(lenv), 2);
            }
            return Math.Sqrt(d2);
        }

        //protected double pbiScalarObj(double[] namda, MoChromosome var)
        //{
        //    //double[] namda = this.weights.get(idx);
        //    double lenv = 0, mul = 0;
        //    for (int i = 0; i < numObjectives; i++)
        //    {
        //        //mul += ((var.objectivesValue[i] - this.idealpoint[i])/(narpoint[i] + 1e-5 - idealpoint[i]))*namda[i];
        //        mul += (var.objectivesValue[i] - this.idealpoint[i]) * namda[i];
        //        lenv += Math.Pow(namda[i], 2);
        //    }
        //    double d1 = mul / Math.Sqrt(lenv);

        //    double d2 = 0;
        //    for (int i = 0; i < numObjectives; i++)
        //    {
        //        //d2 += Math.pow(((var.objectivesValue[i] - this.idealpoint[i])/(narpoint[i] + 1e-5 - idealpoint[i])) - d1 * namda[i]/Math.sqrt(lenv),2);
        //        d2 += Math.Pow((var.objectivesValue[i] - this.idealpoint[i]) - d1 * namda[i] / Math.Sqrt(lenv), 2);
        //    }

        //    return 5 * Math.Sqrt(d2) + d1;

        //    //return projDist(var.objectivesValue,namda) + 5 * perDist(var.objectivesValue,namda);
        //}

    }
}
