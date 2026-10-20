using System;

namespace CultOfComfort.Simulation.Base;

/// <summary>Visitors at the Hearth (spec 2026-10-20 visitors): see <see cref="VisitorBook"/>.</summary>
public sealed partial class BaseState
{
    private long _answeredVisitorSlot = -1; // the last slot whose visitor was answered (taken up or sent on)

    /// <summary>Whoever stands at the Hearth now: this slot's visitor, or the last slot's if still staying; none once answered.</summary>
    private (long Slot, int Offer, DateTimeOffset Leaves, DateTimeOffset Arrives)? VisitorAt(DateTimeOffset now)
    {
        if (HearthLevel < VisitorBook.FromHearth)
        {
            return null;
        }

        var current = VisitorBook.SlotOf(now);
        for (var slot = current; slot >= current - 1; slot--)
        {
            if (slot <= _answeredVisitorSlot)
            {
                break;
            }

            var (arrives, leaves, offer) = VisitorBook.Visit(slot, HearthLevel);
            if (arrives <= now && now < leaves)
            {
                return (slot, offer, leaves, arrives);
            }
        }

        return null;
    }

    private VisitorView? VisitorViewAt(DateTimeOffset now)
    {
        if (VisitorAt(now) is not { } v)
        {
            return null;
        }

        var offer = VisitorBook.Offers[v.Offer];
        return new VisitorView(offer, v.Offer, v.Leaves, VisitorBook.Hash(v.Arrives), _stock.Covers(offer.Asks), v.Slot);
    }

    private CommandOutcome Answer(AnswerVisitor answer, DateTimeOffset now)
    {
        if (VisitorAt(now) is not { } v)
        {
            return CommandOutcome.Reject("Nobody's there. They've gone on their way.");
        }

        var offer = VisitorBook.Offers[v.Offer];
        if (!answer.Accept)
        {
            _answeredVisitorSlot = v.Slot;
            return CommandOutcome.Accept();
        }

        if (!_stock.Covers(offer.Asks))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(offer.Asks)} more.");
        }

        MaterializeEconomy(now);
        _stock -= offer.Asks;
        var given = Refund(offer.Gives); // a gift never overfills the stores
        _towels += offer.Towels;
        if (offer.Villagers > 0)
        {
            _population = Math.Min(Beds(), _population + offer.Villagers); // they stay if there's a bed
        }

        _answeredVisitorSlot = v.Slot;
        return CommandOutcome.Accept(given);
    }

    private void RestoreVisitor(BaseData data) => _answeredVisitorSlot = data.AnsweredVisitorSlot;
}
