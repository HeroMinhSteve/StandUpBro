using System;
using System.Media;
using System.Windows;
using System.Windows.Input;

namespace StandUpBro;

/// <summary>
/// A TopMost modal dialog that displays a random arithmetic problem.
/// The user cannot close it via Alt+F4 or the X button — they MUST
/// type the correct answer and click "Check" (or press Enter).
/// </summary>
public partial class MathChallengeWindow : Window
{
    private readonly int _correctAnswer;

    public MathChallengeWindow()
    {
        InitializeComponent();

        // Generate a random math problem
        (string problem, int answer) = GenerateProblem();
        _correctAnswer = answer;
        ProblemLabel.Text = problem;

        // Focus the answer input when the window loads
        Loaded += (_, _) =>
        {
            AnswerInput.Focus();
            Keyboard.Focus(AnswerInput);

            // Play a system notification sound to grab attention
            SystemSounds.Exclamation.Play();
        };

        // Allow pressing Enter to submit
        AnswerInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                CheckAnswer();
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Block all ways to close without solving
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prevents closing via Alt+F4, the taskbar close, etc.
    /// Only our own code can set _solved = true and close.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // If this close wasn't triggered by a correct answer, cancel it
        if (!_solved)
        {
            e.Cancel = true;
            FeedbackLabel.Text = "Nice try! Solve the problem to dismiss. 😏";
        }

        base.OnClosing(e);
    }

    private bool _solved;

    // ═══════════════════════════════════════════════════════════════════
    //  Problem generation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a random arithmetic problem using +, −, or ×.
    /// Keeps numbers and results small enough to be solvable quickly.
    /// </summary>
    private static (string Problem, int Answer) GenerateProblem()
    {
        var rng = new Random();

        // Pick a random operator: 0 = add, 1 = subtract, 2 = multiply
        int op = rng.Next(3);

        int a, b, answer;
        string symbol;

        switch (op)
        {
            case 0: // Addition: two numbers 10–99
                a = rng.Next(10, 100);
                b = rng.Next(10, 100);
                answer = a + b;
                symbol = "+";
                break;

            case 1: // Subtraction: ensure a ≥ b so the answer is non-negative
                a = rng.Next(20, 100);
                b = rng.Next(1, a);
                answer = a - b;
                symbol = "−";
                break;

            case 2: // Multiplication: keep factors small (2–12)
                a = rng.Next(2, 13);
                b = rng.Next(2, 13);
                answer = a * b;
                symbol = "×";
                break;

            default:
                throw new InvalidOperationException();
        }

        return ($"{a}  {symbol}  {b}  =", answer);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Answer checking
    // ═══════════════════════════════════════════════════════════════════

    private void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        CheckAnswer();
    }

    private void CheckAnswer()
    {
        string input = AnswerInput.Text.Trim();

        if (!int.TryParse(input, out int userAnswer))
        {
            FeedbackLabel.Text = "Enter a valid number.";
            AnswerInput.SelectAll();
            AnswerInput.Focus();
            return;
        }

        if (userAnswer == _correctAnswer)
        {
            _solved = true;
            Close(); // OnClosing will allow it because _solved is true
        }
        else
        {
            FeedbackLabel.Text = "Wrong! Try again. 💪";
            AnswerInput.SelectAll();
            AnswerInput.Focus();
        }
    }
}
