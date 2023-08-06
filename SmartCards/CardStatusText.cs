using IsikReg.Configuration;
using IsikReg.Extensions;
using IsikReg.Properties;
using System.Collections.Generic;
using System.Windows.Media;

namespace IsikReg.SmartCards {
  public class CardStatusText {

    private readonly static SolidColorBrush BLACK = new(Colors.Black);
    private readonly static SolidColorBrush GREEN = new(Colors.Green);
    private readonly static SolidColorBrush RED = new(Colors.Red);
    private readonly static SolidColorBrush GRAY = new(Colors.Gray);

    private readonly VariableStatusMessages format;

    public Property<string> TextProperty { get; } = new("");

    public Property<Brush> ColorProperty { get; } = new(BLACK);

    public Property<bool> CardPresentProperty { get; } = new();

    public Property<State> StatusProperty { get; } = new();

    public CardStatusText(VariableStatusMessages format) {
      this.format = format;

      StatusProperty.PropertyChanged += (o, status) => {
        switch (status) {
          case State.NULL:
            Null();
            break;
          case State.DRIVER_MISSING:
            DriverMissing();
            break;
          case State.WAITING_CARD_READER:
            WaitingForCardReader();
            break;
          case State.WAITING_CARD:
            WaitingCard();
            break;
          case State.UNRESPONSIVE_CARD:
            UnresponsiveCard();
            break;
          case State.PROTOCOL_MISMATCH:
            ProtocolMismatch();
            break;
          case State.CARD_PRESENT:
            CardPresent();
            break;
          case State.READING_CARD:
            ReadingCard();
            break;
          case State.APDU_EXCEPTION:
            Exception();
            break;
          case State.READING_FAILED:
            Failed();
            break;
          case State.READING_SUCCESS:
            Success();
            break;
        }
      };
    }

    public void Bind(CardStatusText other) {
      TextProperty.Bind(other.TextProperty);
      ColorProperty.Bind(other.ColorProperty);
      CardPresentProperty.Bind(other.CardPresentProperty);
      StatusProperty.Bind(other.StatusProperty);
    }

    public void Unbind() {
      TextProperty.Unbind();
      ColorProperty.Unbind();
      CardPresentProperty.Unbind();
      StatusProperty.Unbind();
    }

    public bool IsBound() {
      return TextProperty.IsBound || ColorProperty.IsBound || CardPresentProperty.IsBound || StatusProperty.IsBound;
    }

    private void Set(string text, Brush color, IReadOnlyDictionary<string, object>? props = null) {
      if (props != null) {
        text = format.ToString(props.ValuesAsString(), text);
      }

      TextProperty.Value = text;
      ColorProperty.Value = color;
    }

    // GREEN

    public void Registered(string type, IReadOnlyDictionary<string, object> props) {
      Set(type.FirstCharCapitalize() + " registreeritud!", GREEN, props);
    }


    // BLACK

    private void WaitingForCardReader() {
      Set("Sisesta ID-kaardi lugeja", BLACK);
    }

    public void WaitingCard() {
      Set("Sisesta ID-kaart", BLACK);
    }

    private void ReadingCard() {
      Set("Loen andmeid ID-kaardilt...", BLACK);
    }

    // RED

    public void NotRegistered(IReadOnlyDictionary<string, object> props) {
      Set("Ei Registreeritud!", RED, props);
    }

    public void NotOnTheList(IReadOnlyDictionary<string, object> props) {
      Set("Pole nimekirjas!", RED, props);
    }

    public void Expired(IReadOnlyDictionary<string, object> props) {
      Set("ID-kaart on aegunud!", RED, props);
    }

    private void UnresponsiveCard() {
      Set("ID-kaarti ei tuvastatud!\nSisesta ID-kaart õigesti lugejasse.", RED);
    }

    private void Exception() {
      Set("Ei suutnud ID-kaarti lugeda!\nVigane kiip?", RED);
    }

    private void ProtocolMismatch() {
      Set("Pole ID-kaart?\nVõta kaart lugejast välja.", RED);
    }

    private void Failed() {
      Set("ID-kaardi lugemine ebaõnnestus.\nSisesta ID-kaart uuesti lugejasse.", RED);
    }


    // GRAY


    private void Null() {
      Set("ID-kaardi lugemine pole toetatud", GRAY);
    }

    public void WaitUserInput(IReadOnlyDictionary<string, object> props) {
      Set("Ootan kasutaja sisendit!", GRAY, props);
    }

    public void AlreadyRegistered(string type, IReadOnlyDictionary<string, object> props) {
      Set("On juba " + type.ToLower() + " registreeritud!", GRAY, props);
    }

    private void Success() {
      Set("Andmete lugemine õnnestus.\nPalun oota!", GRAY);
    }

    public void CardPresent() {
      Set("ID-kaart olemas. Palun oota!", GRAY);
    }

    private void DriverMissing() {
      Set("Draiver puudub! Sisesta ID-kaardi\nlugeja ja taaskäivita programm.", GRAY);
    }

  }
}
