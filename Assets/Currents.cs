using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEngine;
using KModkit;

public class Currents : MonoBehaviour
{
    public KMAudio bombaudio;
    public KMBombInfo bomb;
    public KMBombModule module;
    public KMSelectable[] buttons;
    public TextMesh[] texts;

    private List<string> buttonnames = new List<string>() { "ButtonRed", "ButtonGreen", "ButtonBlue", "ButtonYellow" };
    private List<string> altbuttonnames = new List<string>() { "Red", "Green", "Blue", "Yellow" };

    private List<int> screenvalues = new List<int> { 0, 0, 0, 0 };

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleReady = false;

    private bool broken = false; 

    private List<List<char>> LayerA = new List<List<char>>()
    {
        new List<char>(){'R','R','D','D','L' },
        new List<char>(){'D','L','L','R','D' },
        new List<char>(){'E','U','L','L','L' },
        new List<char>(){'U','L','D','U','U' },
        new List<char>(){'U','L','R','E','L' }
    };

    private List<List<char>> LayerB = new List<List<char>>()
    {
        new List<char>(){'D','L','L','R','D' },
        new List<char>(){'D','U','R','R','E' },
        new List<char>(){'R','R','U','U','D' },
        new List<char>(){'R','U','R','E','D' },
        new List<char>(){'R','R','U','U','L' }
    };

    private List<List<char>> LayerC = new List<List<char>>()
    {
        new List<char>(){'D','D','L','L','L' },
        new List<char>(){'D','R','E','L','U' },
        new List<char>(){'D','R','U','L','U' },
        new List<char>(){'R','E','U','U','L' },
        new List<char>(){'U','L','U','R','U' }
    };

    private List<List<List<char>>> layers = new List<List<List<char>>>();

    private int redstart = 0;
    private int greenstart = 0;
    private int bluestart = 0;
    private int yellowstart = 0;

    List<char> redmoves = new List<char>();
    List<char> greenmoves = new List<char>();
    List<char> bluemoves = new List<char>();
    List<char> yellowmoves = new List<char>();
    List<List<char>> moveslists = new List<List<char>>();

    List<int> solution = new List<int>();
    List<int> startingscreen = new List<int>() { 0, 0, 0, 0 };
        

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in buttons)
        {
            KMSelectable pressedButton = button;
            button.OnInteract += delegate () { ButtonPress(pressedButton); return false; };
        }
    }




    // Use this for initialization
    void Start()
    {
        //initialize screen 
        for(var i = 0; i < 4; i++)
        {
            startingscreen[i] = UnityEngine.Random.Range(0, 10);
            texts[i].text = startingscreen[i].ToString();
        }
        Debug.LogFormat("[Currents #{0}] The starting screen values are R=" + startingscreen[0] + ", G=" + startingscreen[1] + ", B=" + startingscreen[2] + ", Y=" + startingscreen[3], moduleId);

        //Picks layer order by calculating the priority values, and reordering the layers from least to greatest, maintaining original order in case of ties
        DetermineLayerOrder();

        //Performs the rotations on the layers
        layers[1] = new List<List<char>>(RotateLayer(layers[1], 1));
        layers[2] = new List<List<char>>(RotateLayer(layers[2], -1));
        Debug.LogFormat("[Currents #{0}] The layers after rotations are: ", moduleId);

        //Displaying the layers with currents for logging
        Debug.LogFormat("[Currents #{0}] Top Layer", moduleId);
        for (var i = 0; i < 5; i++)
        {
            Debug.LogFormat("[Currents #{0}] " + ListToString(layers[0][i]), moduleId);
        }
        Debug.LogFormat("[Currents #{0}] Middle Layer", moduleId);
        for (var i = 0; i < 5; i++)
        {
            Debug.LogFormat("[Currents #{0}] " + ListToString(layers[1][i]), moduleId);
        }
        Debug.LogFormat("[Currents #{0}] Bottom Layer", moduleId);
        for (var i = 0; i < 5; i++)
        {
            Debug.LogFormat("[Currents #{0}] " + ListToString(layers[2][i]), moduleId);
        }

        //Calculates boat starting positions
        FindBoatStartPositions();

        //Determines the list of moves for each boat
        CalculateMoves();

        //Counts the moves for each boat to find the solutions
        if(!broken) FindSolutions();

        moduleReady = true; 
    }

    void DetermineLayerOrder()
    {
        //Choose Layer Order
        //Layer A: AA/2
        //Layer B: D
        //Layer C: Port Plates
        //Order by counts, smallest on top, largest on bottom
        int APV = bomb.GetBatteryCount(Battery.AA)/2 + 10*startingscreen[0] + startingscreen[1];
        int BPV = bomb.GetBatteryCount(Battery.D) + 10*startingscreen[1] + startingscreen[2];
        int CPV = bomb.GetPortPlateCount() + 10*startingscreen[2] + startingscreen[3];
        Debug.LogFormat("[Currents #{0}] The priority values are A=" + APV + ", B=" + BPV + ", C=" + CPV, moduleId);
        IndexedMatrix d1 = new IndexedMatrix { matrix = LayerA, value = APV, name = "Floor A" };
        IndexedMatrix d2 = new IndexedMatrix { matrix = LayerB, value = BPV, name = "Floor B" };
        IndexedMatrix d3 = new IndexedMatrix { matrix = LayerC, value = CPV, name = "Floor C" };
        List<IndexedMatrix> l = new List<IndexedMatrix>() { d1, d2, d3 };  
        IEnumerable<IndexedMatrix> query = l.OrderBy(x => x.value);
        l = new List<IndexedMatrix>(query.ToList());
        layers.Add(l[0].matrix);
        layers.Add(l[1].matrix);
        layers.Add(l[2].matrix);
        Debug.LogFormat("[Currents #{0}] The tower order is " + l[0].name + ", " + l[1].name + ", " + l[2].name + " from top to bottom.", moduleId);
        Debug.LogFormat("[Currents #{0}] " + l[1].name + " will be rotated 90 degrees CW.", moduleId);
        Debug.LogFormat("[Currents #{0}] " + l[2].name + " will be rotated 90 degrees CCW.", moduleId);
    }

    class IndexedMatrix
    {
        public List<List<char>> matrix { get; set; }
        public int value { get; set; }
        public string name { get; set; }
    }
    
    List<List<char>> RotateLayer(List<List<char>> layer, int rotation) 
    {
        //Rotates the input layer by the rotation given
        //The method used to rotate is Transposition and Row Reversal
        //If you need to rotate a matrix CW 90 degrees, you just find the transpose, and then reverse all the rows of the transpose
        //If you need to rotate CCW 90 degrees, you do these steps in opposite order
        //After the layers are rotated, the currents are reassigned to their new directions
        List<List<char>> newlayer = new List<List<char>>(layer);
        //1 is CW -1 is CCW
        switch (rotation)
        {
            case 1:
                newlayer = TransposeMatrix(newlayer);
                newlayer = ReverseRows(newlayer);
                foreach(List<char> l in newlayer)
                {
                    for(var i = 0; i < 5; i++)
                    {
                        switch (l[i])
                        {
                            case 'U':
                                l[i] = 'R';
                                break;
                            case 'R':
                                l[i] = 'D';
                                break;
                            case 'D':
                                l[i] = 'L';
                                break;
                            case 'L':
                                l[i] = 'U';
                                break;
                        }
                    }
                }
                break;
            case -1:
                newlayer = ReverseRows(newlayer);
                newlayer = TransposeMatrix(newlayer);
                foreach (List<char> l in newlayer)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        switch (l[i])
                        {
                            case 'U':
                                l[i] = 'L';
                                break;
                            case 'R':
                                l[i] = 'U';
                                break;
                            case 'D':
                                l[i] = 'R';
                                break;
                            case 'L':
                                l[i] = 'D';
                                break;
                        }
                    }
                }
                break;
        }
        return newlayer;
    }

    //Basic matrix transpose
    List<List<char>> TransposeMatrix(List<List<char>> matrix)
    {
        List<List<char>> newmatrix = new List<List<char>>();
        for (var i = 0; i < 5; i++)
        {
            newmatrix.Add(new List<char>(5)); 
            for (var j = 0; j < 5; j++)
            {
                newmatrix[i].Add(matrix[j][i]);
            }
        }
        return newmatrix;
    }

    //Basic row reversal, takes advantage of List method Reverse()
    List<List<char>> ReverseRows(List<List<char>> matrix)
    {
        List<List<char>> newmatrix = new List<List<char>>(matrix);
        List<char> l = new List<char>();
        for (var i = 0; i < 5; i++)
        {
            l = new List<char>(newmatrix[i]);
            l.Reverse();
            newmatrix[i] = new List<char>(l);
        }

        return newmatrix;
    }

    void FindBoatStartPositions()
    {
        //Choose Boat Start Positions
        //For each color, add the starting screen value as well
        //Red: Start from 0, add 1 for R, E, D in serial, add 3 for ports with R in the name (RJ,Serial,Parallel,RCA), add 5 for indicators with R (FRK,FRQ,CAR,CLR,TRN)
        redstart += bomb.GetSerialNumber().Count(x => x == 'R' | x == 'E' | x == 'D');
        redstart += 3*(bomb.GetPortCount("StereoRCA") + bomb.GetPortCount("RJ45") + bomb.GetPortCount("Parallel") + bomb.GetPortCount("Serial"));
        redstart += 5*(bomb.GetIndicators().Count(x => x == "FRK" | x == "FRQ" | x == "CAR" | x == "CLR" | x == "TRN"));
        redstart += startingscreen[0];
        redstart %= 25;

        //Green: Sum of Ups and Downs (4 for each) in Top Layer and sum of odd numbers in serial
        foreach (int i in bomb.GetSerialNumberNumbers())
        {
            if(i % 2 == 1)
            {
                greenstart += i;
            }
        }
        foreach(List<char> l in layers[0])
        {
            foreach(char c in l)
            {
                if (c == 'U' || c == 'D') greenstart+=4;
            }
        }
        greenstart += startingscreen[1];
        greenstart %= 25;

        //Blue: Sum of Lefts and Rights in Bottom Layer (3 for each) and sum of even numbers in serial
        foreach (int i in bomb.GetSerialNumberNumbers())
        {
            if (i % 2 == 0)
            {
                bluestart += i;
            }
        }
        foreach (List<char> l in layers[2])
        {
            foreach (char c in l)
            {
                if (c == 'L' || c == 'R') bluestart+=3;
            }
        }
        bluestart += startingscreen[2];
        bluestart %= 25;

        //Yellow: Sum of all letters in A1Z26 in serial number
        foreach (char c in bomb.GetSerialNumberLetters())
        {
            yellowstart += c - 'A' + 1 ;
        }
        yellowstart += startingscreen[3];
        yellowstart %= 25;

        Debug.LogFormat("[Currents #{0}] The boats start at R:(" + ((redstart - redstart % 5) / 5) + "," + redstart % 5 + "), G:(" + ((greenstart - greenstart % 5) / 5) + "," + greenstart % 5 + "), B:(" + ((bluestart - bluestart % 5) / 5) + "," + bluestart % 5 + "), Y:(" + ((yellowstart - yellowstart % 5) / 5) + "," + yellowstart % 5 + ").", moduleId);
    }

    //Finds the move list for each boat
    void CalculateMoves()
    {
        //initialize moveslists
        moveslists.Add(redmoves);
        moveslists.Add(greenmoves);
        moveslists.Add(bluemoves);
        moveslists.Add(yellowmoves);

        int[] positions = new int[4] { redstart, greenstart, bluestart, yellowstart };
        bool finished = false;
        char move;
        int pos = 0;
        int iter = 0; 

        //This while loop continues until all boats are removed from the tower
        while (!finished)
        {
            //failsafe, but also the iterations can be used to count the number of moves before a boat crashed
            iter++;
            if (iter > 300)
            {
                Debug.LogFormat("[Currents #{0}] Too many iterations, just press submit!", moduleId);
                broken = true;
                foreach(var i in texts)
                {
                    i.text = "?";
                }
                return;
            }

            //This loops through the current positions of the boats to check for crashes, which occur if two or more boats have the same position
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 4; j++)
                {
                    if (i != j)
                    {
                        if (positions[i] == positions[j] && (positions[i] != -1 || positions[j] != -1))
                        {
                            positions[i] = positions[j] = -1;
                            Debug.LogFormat("[Currents #{0}] Oh no! The " + altbuttonnames[i] + " and " + altbuttonnames[j] + " boats crashed after " + (iter-1) + " moves!", moduleId);
                        }
                    }
                }
            }

            //This calculates each boats position and records the move in the moveslist for that boat
            //For this I decided to use just the values 0-24 as the positions, since the currents will never allow a boat to leave the grid
            //it makes calculating the position easier, and no need to track coordinates, also for each floor I just add 100 to the position
            //If the position is greater or equal to 300 then the boat is finished
            //If the current move is E then it goes to the next floor, otherwise it just follows the current
            //Once a boat is removed or has crashed, it's move will always return F which means finished
            for (var i = 0; i < 4; i++)
            {
                pos = positions[i];
                move = GetDirection(pos % 100, layers[(pos - pos % 100) / 100]);
                moveslists[i].Add(move);
                switch (move)
                {
                    case 'U':
                        pos = pos - 5;
                        break;
                    case 'R':
                        pos = pos + 1;
                        break;
                    case 'D':
                        pos = pos + 5;
                        break;
                    case 'L':
                        pos = pos - 1;
                        break;
                    case 'E':
                        pos += 100;
                        if (pos >= 300) pos = -1;
                        break;
                }
                positions[i] = pos; 
            }

            //Loop completes when all boats moves return F
            finished = moveslists[0].Last() == 'F' && moveslists[1].Last() == 'F' && moveslists[2].Last() == 'F' && moveslists[3].Last() == 'F';
        }
    }

    //Returns the direction given by the position in the corresponding layer or F if the boat is finished
    char GetDirection(int position, List<List<char>> layer)
    {
        if (position == -1) return 'F';
        return layer[(position - position % 5) / 5][position % 5];
    }

    //Counts the moves for each boat required for the solution
    void FindSolutions()
    {
        int redsolution = moveslists[0].Count(x => x == 'U');
        int greensolution = moveslists[1].Count(x => x == 'R');
        int bluesolution = moveslists[2].Count(x => x == 'D');
        int yellowsolution = moveslists[3].Count(x => x == 'L');
        solution = new List<int>() { redsolution, greensolution, bluesolution, yellowsolution };
        Debug.LogFormat("[Currents #{0}] The solutions are R:" + solution[0] + ", G:" + solution[1] + ", B:" + solution[2] + ", Y:" + solution[3], moduleId);
    }

    private bool buttonpressed = false;

    void ButtonPress(KMSelectable button)
    {
        bombaudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);
        button.AddInteractionPunch();
        bool moduleSolved = false;
        var index = -1;
        if (moduleReady)
        {
            if (broken)
            {
                bombaudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, this.transform);
                Debug.LogFormat("[Currents #{0}] Module broke while trying to follow boats! Solved!", moduleId);
                module.HandlePass();
                moduleReady = false;
                return;
            }
            if (button.name == "ButtonSubmit")
            {
                for (var i = 0; i < 4; i++)
                {
                    moduleSolved = solution[i] == screenvalues[i];
                }
                if (moduleSolved)
                {
                    bombaudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, this.transform);
                    Debug.LogFormat("[Currents #{0}] Nicely done tracking those boats! Solved!", moduleId);
                    module.HandlePass();
                    moduleReady = false;
                }
                else
                {
                    module.HandleStrike();
                    Debug.LogFormat("[Currents #{0}] The Defuser pressed submit but the solution is incorrect! Strike!", moduleId);
                }
            }
            else if(button.name == "ButtonReset")
            {
                buttonpressed = false; 
                for(var i = 0; i < 4; i++)
                {
                    screenvalues[i] = 0;
                    texts[i].text = startingscreen[i].ToString();
                }
            }
            else
            {
                if (!buttonpressed)
                {
                    buttonpressed = true; 
                    for (var i = 0; i < 4; i++)
                    {
                        texts[i].text = "0";
                    }
                }
                index = buttonnames.IndexOf(button.name);
                if(screenvalues[index] < 99) texts[index].text = (screenvalues[index] += 1).ToString();
            }
        }
    }

    //Hacky method to convert serial characters to row,col mod 5
    private int ConvToPosSmall(char serialelement)
    {
        int num = serialelement - '0';
        if (num > 9)
        {
            num += '0' - 'A' + 1;
        }
        while (num > 9)
        {
            num -= 10;
        }
        while (num > 4)
        {
            num -= 5;
        }
        return num;
    }

    private int mod(int x, int m)
    {
        return (x % m + m) % m;
    }

    string ListToString(List<string> l)
    {
        string str = "";
        foreach (var s in l) str += s;
        return str;
    }

    string ListToString(List<int> l)
    {
        string str = "";
        foreach (var s in l) str += s.ToString();
        return str;
    }

    string ListToString(List<char> l)
    {
        string str = "";
        foreach (var s in l) str += s.ToString();
        return str;
    }

    //twitch plays
    private bool inputIsValid(string cmd)
    {
        string[] validstuff = { "r", "g", "b", "y", "submit", "clear", "c", "reset" };
        if (validstuff.Contains(cmd.ToLower()))
        {
            return true;
        }
        return false;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <R/G/B/Y/submit/clear> [Presses the specified button]. You can also string presses together i.e. press R G B Y, press R,G,B,Y. You can also use numbers to simplify the expression i.e. press R 4 G 3.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ', ',');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            for (int i = 1; i < parameters.Length; i++)
            {
                if (inputIsValid(parameters[i]))
                {
                    yield return null;
                    if (parameters[i].ToLower().Equals("r"))
                    {
                        buttons[0].OnInteract();
                    }
                    else if (parameters[i].ToLower().Equals("g"))
                    {
                        buttons[1].OnInteract();
                    }
                    else if (parameters[i].ToLower().Equals("b"))
                    {
                        buttons[2].OnInteract();
                    }
                    else if (parameters[i].ToLower().Equals("y"))
                    {
                        buttons[3].OnInteract();
                    }
                    else if (parameters[i].ToLower().Equals("submit"))
                    {
                        buttons[4].OnInteract();
                    }
                    else if (parameters[i].ToLower().Equals("clear") || parameters[i].ToLower().Equals("c") || parameters[i].ToLower().Equals("reset"))
                    {
                        buttons[5].OnInteract();
                    }
                }
                else if (i > 1 && parameters[i].All(char.IsNumber))
                {
                    if (inputIsValid(parameters[i - 1]))
                    {
                        var value = 0;
                        int.TryParse(parameters[i], out value);
                        for (int j = 0; j < value-1; j++)
                        {
                            yield return null;
                            if (parameters[i-1].ToLower().Equals("r"))
                            {
                                buttons[0].OnInteract();
                            }
                            else if (parameters[i-1].ToLower().Equals("g"))
                            {
                                buttons[1].OnInteract();
                            }
                            else if (parameters[i-1].ToLower().Equals("b"))
                            {
                                buttons[2].OnInteract();
                            }
                            else if (parameters[i-1].ToLower().Equals("y"))
                            {
                                buttons[3].OnInteract();
                            }
                        }
                    }
                }
            }
            yield break;
        }
    }

}    