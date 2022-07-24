using System.Net;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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

        public PaymentViewModel(IObservable<string> incomingContent, IMutableAddressHost mutableAddressHost, ContentChecker<string> contentChecker, Func<decimal, bool> isAmountValid)
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
            HasNewContent = contentChecker.HasNewContent;

            if (Services.UiConfig.AutoPaste)
            {
	            contentChecker.NewContent
		            .Subscribe(content => MutableAddressHost.Text = content)
		            .DisposeWith(disposables);
            }

            PasteCommand = ReactiveCommand.Create(() =>
            {
                MutableAddressHost.Text = clipboardContent.Value;
            }).DisposeWith(disposables);

            var validAmount = this.WhenAnyValue(x => x.Amount).Select(x => x > 0);

            this.ValidationRule(
                viewModel => viewModel.Amount, validAmount.Skip(1),
                "Amount should be greater than 0");

            this.ValidationRule(
	            x => x.Amount,
	            isAmountValid,
	            "Insufficient funds to cover the amount requested");
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
