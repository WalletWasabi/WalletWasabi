using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels
{
    public class PaymentViewModel : ReactiveValidationObject, IDisposable
    {
        private string address = "";
        private decimal amount;
        private readonly CompositeDisposable disposables = new();

        public PaymentViewModel(IObservable<string> incomingContent, IMutableAddressHost mutableAddressHost, ContentChecker<string> contentChecker)
        {
            MutableAddressHost = mutableAddressHost;
            MutableAddressHost.ParsedAddress.Subscribe(a =>
            {
                if (a is not null)
                {
                    Address = a.BtcAddress;

                    if (a.Amount is not null)
                    {
                        Amount = a.Amount.Value;
                    }

                    if (a.EndPoint is not null)
                    {
	                    EndPoint = a.EndPoint;
                    }
                }
            }).DisposeWith(disposables);

            var clipboardContent = new BehaviorSubject<string>("");
            incomingContent.Subscribe(clipboardContent).DisposeWith(disposables);
            HasNewContent = contentChecker.ActivatedWithNewContent;
            PasteCommand = ReactiveCommand.Create(() =>
            {
                MutableAddressHost.Text = clipboardContent.Value;
            }).DisposeWith(disposables);

            this.ValidationRule(
                viewModel => viewModel.Amount, x => x > 0,
                "Amount should be greater than 0");
        }

        public Uri? EndPoint { get; set; }

        public ReactiveCommand<Unit, Unit> PasteCommand { get; }

        public IObservable<bool> HasNewContent { get; }

        public IMutableAddressHost MutableAddressHost { get; }

        public decimal Amount
        {
            get => amount;
            set => this.RaiseAndSetIfChanged(ref amount, value);
        }

        public string Address
        {
            get => address;
            set => this.RaiseAndSetIfChanged(ref address, value);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
